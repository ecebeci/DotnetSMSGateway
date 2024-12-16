# Dotnet SMS Gateway

## Overview

Dotnet SMS Gateway is a lightweight application for sending and receiving SMS messages using AT Commands, with Redis Pub/Sub integration for message queuing. It is designed for simplicity and extensibility, enabling SMS handling via serial communication with a GSM modem.

## Features

- **AT Command SMS Sending**: Send SMS messages using AT commands.
- **Redis Pub/Sub Integration**: Manage message queues for sending and receiving SMS.

### Missing Features

- **UCS-2 Encoding**: Reading and sending UCS-2 (Unicode) encoded messages.
- **Message Queue Persistence**: Persistent message queuing support.

## Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/ecebeci/Dotnet-SMS-Gateway.git
   ```

2. Navigate to the project directory:

   ```bash
   cd Dotnet-SMS-Gateway
   ```

3. Install dependencies:

   ```bash
   dotnet restore
   ```

4. Configure the `.env` file:

   Copy `.env.example` to `.env` and update the following environment variables:
   - `MODEM_PORTNAME`: The port name for the GSM modem (e.g., `COM3`).
   - `REDIS_CONNECTION_STRING`: Redis connection string (e.g., `localhost:6379`).
   - `REDIS_CHANNEL_RECEIVED`: Redis channel name for received messages.
   - `REDIS_CHANNEL_SEND`: Redis channel name for sending messages.

## Usage

1. Start the program:

   ```bash
   dotnet run
   ```

2. Interact with the application:
   - Type commands directly to the serial interface.
   - Publish messages to the Redis send channel to send SMS.

3. Stop the program:
   - Type `QUIT` and press Enter.

### Example AT Commands

Here are some commonly used AT commands for interacting with the GSM modem:

1. **Set SMS storage to SIM card**:
   - Command: `AT+CPMS="ME"`
   - Ensures that SMS messages are stored on the SIM card.

2. **Set text mode for SMS**:
   - Command: `AT+CMGF=1`
   - Switches the modem to text mode for SMS operations.

3. **Read all messages**:
   - Command: `AT+CMGL="ALL"`
   - Retrieves all messages stored on the SIM card.

4. **Send an SMS**:
   - Command: `AT+CMGS="<phone_number>"`
   - After entering the command, type the message content, then press `Ctrl+Z` and enter to send.

## Contributing

Contributions are welcome! If you'd like to help improve Sound Syntax, please fork the repository and submit a pull request.

## License

This project is licensed under the GNU General Public License v3.0. See the [LICENSE](https://www.gnu.org/licenses/gpl-3.0.html) file for details.