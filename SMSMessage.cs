using System.Text.Json;
using StackExchange.Redis;

namespace SMS_Gateway;
class SMSMessage(string content, DateTime timestamp, string? sender = null, string? receiver = null)
{
  public string Content { get; } = content;
  public DateTime Timestamp { get; } = timestamp;
  public string? Sender { get; } = sender;
  public string? Receiver { get; } = receiver;

  public static explicit operator RedisValue(SMSMessage v)
  {
    return JsonSerializer.Serialize(v);
  }
}
