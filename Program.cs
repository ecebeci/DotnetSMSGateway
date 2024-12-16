/* 
SMS Gateway application that uses Redis pub/sub to send and receive SMS messages using AT commands.
Copyright(C) 2024 - Emre Cebeci

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>. 
*/
using System.IO.Ports;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Text;

namespace SMS_Gateway;
public class Program
{
  static SerialPort? _serialPort;
  static ISubscriber? _pubsub;
  static RedisChannel _receivedChannel;
  static RedisChannel _sendChannel;
  static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();
  public static void Main()
  {
    EnvReader.Load(".env");
    InitializeSerialPort();
    InitializeRedis();
    Console.WriteLine("Type QUIT to exit");
    var stringComparer = StringComparer.OrdinalIgnoreCase;
    while (true)
    {
      var message = Console.ReadLine() ?? string.Empty;

      if (stringComparer.Equals("quit", message))
      {
        break;
      }
      else
      {
        _logger.LogInformation("PC: {Message}", message);
        _serialPort?.WriteLine(
            string.Format($"{message}\r\n"));
      }
    }
    _serialPort?.Close();
  }

  public static void InitializeSerialPort()
  {
    _serialPort = new SerialPort
    {
      PortName = Environment.GetEnvironmentVariable("MODEM_PORTNAME"),
      BaudRate = 9600,
      Parity = Parity.None,
      DataBits = 8,
      StopBits = StopBits.One,
      Handshake = Handshake.None,
      ReadTimeout = Timeout.Infinite,
      WriteTimeout = 500
    };
    _logger.LogInformation("Open Serial port");
    try
    {
      _serialPort.Open();
    }
    catch (Exception ex)
    {
      _logger.LogCritical(ex, "Error opening serial port: {Message}", ex.Message);
      return;
    }
    _logger.LogInformation("Select Message Storage to ME (SIM card)");
    _serialPort.WriteLine("AT+CPMS=\"ME\"\r\n");
    _logger.LogInformation("Select Message Format to Text Mode ");
    _serialPort.WriteLine("AT+CMGF=1\r\n");
    // _logger.LogInformation("Select TE Character Set to Unicode");
    // _serialPort.WriteLine("AT+CSCS=\"UCS2\"\r\n");
    // _logger.LogInformation("Set SMS Text Mode Parameters https://en.wikipedia.org/wiki/Data_Coding_Scheme ");
    _serialPort.WriteLine("AT+CSMP=17,167,0,0\r\n");
    _logger.LogInformation("Add data received event handler");
    _serialPort.DataReceived += SerialPortDataReceived;
  }

  private static void SerialPortDataReceived(object sender, SerialDataReceivedEventArgs e)
  {
    string message = _serialPort?.ReadExisting() ?? string.Empty;
    if (message.Contains("+CMTI: \"ME\""))
    {
      _logger.LogInformation("New message received. Reading all messages...");
      _serialPort?.WriteLine("AT+CMGL=\"ALL\"\r\n");
      return;
    }
    if (message.Contains("+CMGL:"))
    {
      _logger.LogInformation("Remove unread messages");
      _serialPort?.WriteLine("AT+CMGD=,2\r\n");
      _logger.LogInformation("Handling message");
      HandleReceivedMessage(message);
      return;
    }
    _logger.LogInformation("Modem: {Message}", message.Trim());
  }
  public static void HandleReceivedMessage(string messageText)
  {
    ParseReceivedMessage(messageText, out var messages);
    messages.ForEach(m => _pubsub?.Publish(_receivedChannel, (RedisValue)m));
  }
  static void ParseReceivedMessage(string message, out List<SMSMessage> output)
  {
    output = [];
    try
    {
      var indexStart = message.IndexOf(':') + 2;
      do
      {
        var indexEnd = message.IndexOf(',', indexStart);
        var index = message[indexStart..indexEnd];

        var statusStart = message.IndexOf('"', indexEnd) + 1;
        var statusEnd = message.IndexOf('"', statusStart);
        var status = message[statusStart..statusEnd];

        var senderStart = message.IndexOf("\",\"", statusStart) + 3;
        var senderEnd = message.IndexOf('\"', senderStart);
        // var sender = ConvertUCS2HexToUtf8(message[senderStart..senderEnd]);
        var sender = message[senderStart..senderEnd];

        int unknownValStart = senderEnd + 2;
        int unknownValEnd = message.IndexOf(',', unknownValStart);
        var unknownVal = message[unknownValStart..unknownValEnd];

        var dateTimeStart = message.IndexOf('"', unknownValEnd) + 1;
        var dateTimeEnd = message.IndexOf('"', dateTimeStart);
        var dateTime = message[dateTimeStart..dateTimeEnd];

        int contentStart = message.IndexOf('\n', dateTimeEnd) + 1;
        int contentEnd = message.IndexOf('\r', contentStart);
        var content = message[contentStart..contentEnd];

        _logger.LogInformation("READING Index: {Index}, Sender: {Sender}, Unknown Value: {UnknownVal}, DateTime: {DateTime}, Content: {Content}", index, sender, unknownVal, dateTime, content);
        if (status != "REC UNREAD")
        {
          indexStart = message.IndexOf(':', contentEnd) + 2;
          _logger.LogWarning("Message is not unread. It has already been read. Skipping...");
          continue;
        }

        output.Add(new SMSMessage(content, DateTime.Now, sender, null));
        indexStart = message.IndexOf(':', contentEnd) + 2;
      }
      while (indexStart > 2);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error parsing message: {Message}", ex.Message);
    }
  }

  static void SendMessage(SMSMessage message)
  {
    try
    {
      if (message.Receiver == null)
      {
        _logger.LogError("Receiver is null");
        return;
      }
      // string receiver = ConvertToUCS2(message.Receiver);
      // string ucs2Message = ConvertToUCS2(message.Content);
      _serialPort?.WriteLine($"AT+CMGS=\"{message.Receiver}\"\r");
      _serialPort?.WriteLine($"{message.Content}\x1A");
      _logger.LogInformation("Message sent to {Receiver}: {Message}", message.Receiver, message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending message: {Message}", ex.Message);
    }
  }
  static string ConvertToUCS2(string input)
  {
    return string.Concat(input.Select(c => ((int)c).ToString("X4")));
  }

  static void InitializeRedis()
  {
    var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ??
     throw new ArgumentNullException("REDIS_CONNECTION_STRING");
    _logger.LogInformation("Connecting to Redis: {ConnectionString}", redisConnectionString);
    var _redis = ConnectionMultiplexer.Connect(redisConnectionString);

    _receivedChannel = new RedisChannel(Environment.GetEnvironmentVariable("REDIS_CHANNEL_RECEIVED") ??
    throw new ArgumentNullException("REDIS_CHANNEL_RECEIVED"), RedisChannel.PatternMode.Literal);
    _logger.LogInformation("Subscribing to channel {Channel}", _receivedChannel);

    _sendChannel = new RedisChannel(Environment.GetEnvironmentVariable("REDIS_CHANNEL_SEND") ?? throw new ArgumentNullException("REDIS_CHANNEL_SEND"), RedisChannel.PatternMode.Literal);
    _logger.LogInformation("Subscribing to channel {Channel}", _sendChannel);

    _pubsub = _redis.GetSubscriber();
    _pubsub.Subscribe(_sendChannel, static (channel, message) =>
    {
      try
      {
        if (string.IsNullOrWhiteSpace(message.ToString()))
        {
          return;
        }
        var smsMessage = JsonSerializer.Deserialize<SMSMessage>(message.ToString());
        if (smsMessage == null)
        {
          return;
        }

        SendMessage(smsMessage);
      }
      catch (JsonException ex)
      {
        _logger.LogError(ex, "Error deserializing message on channel {Channel}", channel);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error processing message on channel {Channel}: {Message}", channel, ex.Message);
      }
    });
  }

  static string ConvertUCS2HexToUtf8(string ucs2Hex)
  {
    byte[] ucs2Bytes = new byte[ucs2Hex.Length / 2];
    for (int i = 0; i < ucs2Hex.Length; i += 2)
    {
      ucs2Bytes[i / 2] = Convert.ToByte(ucs2Hex.Substring(i, 2), 16);
    }

    string ucs2String = Encoding.BigEndianUnicode.GetString(ucs2Bytes);

    return ucs2String;
  }
}
