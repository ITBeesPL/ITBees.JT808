﻿using System.Net;
using System.Net.Sockets;
using System.Text;
using ITBees.Interfaces.Platforms;
using ITBees.JT808.Interfaces;
using Microsoft.Extensions.Logging;

namespace JT808ServerApp
{
    public class JT808Server : IJT808Server
    {
        private readonly IPlatformSettingsService _platformSettingsService;
        private readonly IGpsWriteRequestLogSingleton _gpsWriteRequestLogSingleton;
        private readonly IGpsDeviceAuthorizationSingleton _gpsDeviceAuthorizationSingleton;
        private readonly ILogger<JT808Server> _logger;
        private readonly int _port;
        private TcpListener _listener;
        private readonly object _fileLock = new object();
        private Dictionary<string, TcpClient> _authorizedDevices = new Dictionary<string, TcpClient>();
        private ushort _serialNumber = 0;

        public JT808Server(IPlatformSettingsService platformSettingsService,
            IGpsWriteRequestLogSingleton gpsWriteRequestLogSingleton,
            IGpsDeviceAuthorizationSingleton gpsDeviceAuthorizationSingleton,
            ILogger<JT808Server> logger)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _platformSettingsService = platformSettingsService;
            _gpsWriteRequestLogSingleton = gpsWriteRequestLogSingleton;
            _gpsDeviceAuthorizationSingleton = gpsDeviceAuthorizationSingleton;
            _logger = logger;
            _port = Convert.ToInt32(_platformSettingsService.GetSetting("JT808_port"));
            _listener = new TcpListener(IPAddress.Any, _port);
            _logger.LogInformation($"Created class JT808 server on port : {_port}");
        }

        public async Task StartAsync()
        {
            var format = $"Start sync - JT808 server on port {_port}";
            Console.WriteLine(format);
            _logger.LogInformation(format);
            _listener.Start();
            while (true)
            {
                var value = $"received request {DateTime.Now}";
                Console.WriteLine(value);
                _logger.LogInformation(value);
                var client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (var networkStream = client.GetStream())
                {
                    var buffer = new byte[1024];
                    var byteList = new List<byte>();

                    while (true)
                    {
                        int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                            break;

                        byteList.AddRange(buffer.Take(bytesRead));

                        while (byteList.Contains(0x7E))
                        {
                            int startIndex = byteList.IndexOf(0x7E);
                            int endIndex = byteList.IndexOf(0x7E, startIndex + 1);
                            if (endIndex == -1)
                                break;

                            var messageBytes = byteList.Skip(startIndex).Take(endIndex - startIndex + 1).ToArray();
                            byteList.RemoveRange(0, endIndex + 1);

                            var data = ProcessJT808Message(messageBytes);
                            if (data != null)
                            {
                                var response = HandleMessage(data, client);
                                if (response != null)
                                {
                                    await networkStream.WriteAsync(response, 0, response.Length);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                client.Close();
            }
        }

        private byte[] ProcessJT808Message(byte[] message)
        {
            if (message.Length < 2 || message[0] != 0x7E || message[^1] != 0x7E)
                return null;

            var content = new byte[message.Length - 2];
            Array.Copy(message, 1, content, 0, content.Length);

            content = Unescape(content);

            var checksum = content[^1];
            var data = new byte[content.Length - 1];
            Array.Copy(content, 0, data, 0, data.Length);

            byte calculatedChecksum = 0;
            foreach (var b in data)
            {
                calculatedChecksum ^= b;
            }

            if (checksum != calculatedChecksum)
                return null;

            return data;
        }

        private byte[] Unescape(byte[] data)
        {
            var result = new List<byte>();
            int i = 0;
            while (i < data.Length)
            {
                if (data[i] == 0x7D)
                {
                    if (i + 1 < data.Length)
                    {
                        if (data[i + 1] == 0x02)
                        {
                            result.Add(0x7E);
                            i += 2;
                        }
                        else if (data[i + 1] == 0x01)
                        {
                            result.Add(0x7D);
                            i += 2;
                        }
                        else
                        {
                            result.Add(data[i]);
                            i++;
                        }
                    }
                    else
                    {
                        result.Add(data[i]);
                        i++;
                    }
                }
                else
                {
                    result.Add(data[i]);
                    i++;
                }
            }
            return result.ToArray();
        }

        private byte[] Escape(byte[] data)
        {
            var result = new List<byte>();
            foreach (var b in data)
            {
                if (b == 0x7E)
                {
                    result.Add(0x7D);
                    result.Add(0x02);
                }
                else if (b == 0x7D)
                {
                    result.Add(0x7D);
                    result.Add(0x01);
                }
                else
                {
                    result.Add(b);
                }
            }
            return result.ToArray();
        }

        private byte[] HandleMessage(byte[] data, TcpClient client)
        {
            string base64String = Convert.ToBase64String(data);
            var gpsData = new GpsData() { RequestBody = base64String, Received = DateTime.Now };

            try
            {
                if (data.Length < 12)
                    return null;

                ushort msgId = ReadUInt16BigEndian(data, 0);
                ushort msgBodyProps = ReadUInt16BigEndian(data, 2);
                string deviceId = BCDToString(data, 4, 6);
                ushort msgSerialNumber = ReadUInt16BigEndian(data, 10);
                gpsData.DeviceId = deviceId;
                var msgBody = new byte[data.Length - 12];
                Array.Copy(data, 12, msgBody, 0, msgBody.Length);

                switch (msgId)
                {
                    case 0x0100:
                        return HandleTerminalRegistration(msgSerialNumber, deviceId, msgBody, gpsData);
                    case 0x0102:
                        return HandleAuthentication(msgSerialNumber, deviceId, msgBody, gpsData);
                    case 0x0002:
                        gpsData.RequestBody = "Create universal response " + gpsData.RequestBody;
                        _gpsWriteRequestLogSingleton.Write(gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 0, deviceId);
                    case 0x0200:
                        HandleLocationReport(deviceId, msgBody, gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 0, deviceId);
                    case 0x0001:
                        gpsData.RequestBody = "Break - 0x0001" + gpsData.RequestBody;
                        _gpsWriteRequestLogSingleton.Write(gpsData);
                        break;
                    case 0x0300:
                        HandleTextMessage(deviceId, msgBody, gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 0, deviceId);
                    case 0x0500:
                        HandleControlResponse(deviceId, msgBody, gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 0, deviceId);
                    case 0x0F01:
                        return HandleTimeSyncRequest(msgSerialNumber, deviceId, gpsData);
                    default:
                        gpsData.RequestBody = "Default not handled case - take a look at it" + gpsData.RequestBody;
                        _gpsWriteRequestLogSingleton.Write(gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 3, deviceId);
                }
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, base64String);
                throw e;
            }
        }

        private byte[] HandleTerminalRegistration(ushort msgSerialNumber, string deviceId, byte[] msgBody, GpsData gpsData)
        {
            gpsData.RequestBody = "HandleTerminalRegistration" + gpsData.RequestBody;

            int index = 0;

            // Parse Province ID (2 bytes)
            ushort provinceId = ReadUInt16BigEndian(msgBody, index);
            index += 2;

            // Parse City ID (2 bytes)
            ushort cityId = ReadUInt16BigEndian(msgBody, index);
            index += 2;

            // Parse Manufacturer ID (5 bytes)
            string manufacturerId = Encoding.ASCII.GetString(msgBody, index, 5).Trim('\0');
            index += 5;

            // Parse Terminal Model (20 bytes)
            string terminalModel = Encoding.ASCII.GetString(msgBody, index, 20).Trim('\0');
            index += 20;

            // Parse Terminal ID (7 bytes)
            string terminalId = Encoding.ASCII.GetString(msgBody, index, 7).Trim('\0');
            index += 7;

            // Parse License Plate Color (1 byte)
            byte plateColor = msgBody[index];
            index += 1;

            try
            {
                // Parse Vehicle Identification Number (Variable length)
                string vin = Encoding.GetEncoding("GBK").GetString(msgBody, index, msgBody.Length - index);

                // Store the VIN in gpsData or another appropriate data structure
                gpsData.VIN = vin;

                _logger.LogInformation($"Terminal Registered: DeviceId={deviceId}, VIN={vin}, TerminalId={terminalId}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            // Send registration response
            ushort responseMsgId = 0x8100;
            var responseBody = new List<byte>();

            // Add the message serial number from the registration message
            responseBody.Add((byte)(msgSerialNumber >> 8));
            responseBody.Add((byte)(msgSerialNumber & 0xFF));

            var isAuthorized = _gpsDeviceAuthorizationSingleton.IsAuthorized(deviceId, terminalModel, terminalId, gpsData.VIN);
            // Result: 0 for success
            if (isAuthorized)
                responseBody.Add(0x00);
            else
                responseBody.Add(0x01);

            // Authentication code (could be dynamically generated)
            string authCode = "AUTH_CODE";
            byte[] authCodeBytes = Encoding.ASCII.GetBytes(authCode);
            responseBody.AddRange(authCodeBytes);

            var responseData = BuildJT808Message(responseMsgId, deviceId, responseBody.ToArray());
            _gpsWriteRequestLogSingleton.Write(gpsData);
            return responseData;
        }


        private byte[] HandleAuthentication(ushort msgSerialNumber, string deviceId, byte[] msgBody, GpsData gpsData)
        {
            var authCode = Encoding.ASCII.GetString(msgBody);
            gpsData.RequestBody = "Handle authentication" + gpsData.RequestBody;
            _gpsWriteRequestLogSingleton.Write(gpsData);
            return CreateUniversalResponse(msgSerialNumber, 0x0102, 0, deviceId);
        }

        private void HandleLocationReport(string deviceId, byte[] msgBody, GpsData gpsData)
        {
            uint alarmFlag = ReadUInt32BigEndian(msgBody, 0);
            uint status = ReadUInt32BigEndian(msgBody, 4);
            uint latitude = ReadUInt32BigEndian(msgBody, 8);
            uint longitude = ReadUInt32BigEndian(msgBody, 12);
            ushort altitude = ReadUInt16BigEndian(msgBody, 16);
            ushort speed = ReadUInt16BigEndian(msgBody, 18);
            ushort direction = ReadUInt16BigEndian(msgBody, 20);
            string time = BCDToString(msgBody, 22, 6);

            gpsData.DeviceId = deviceId;
            gpsData.Latitude = latitude / 1e6;
            gpsData.Longitude = longitude / 1e6;
            gpsData.Speed = speed / 10.0;
            gpsData.Direction = direction;
            gpsData.Timestamp = DateTime.ParseExact(time, "yyMMddHHmmss", null);
            gpsData.AlarmFlag = alarmFlag;
            gpsData.Status = status;
            gpsData.Altitude = altitude;
            gpsData.RequestBody = "Handle location report" + gpsData.RequestBody;

            ParseAdditionalData(msgBody.Skip(28).ToArray(), gpsData);

            _gpsWriteRequestLogSingleton.Write(gpsData);
        }

        private void ParseAdditionalData(byte[] data, GpsData gpsData)
        {
            int index = 0;
            while (index < data.Length)
            {
                byte infoId = data[index++];
                byte infoLength = data[index++];
                byte[] infoContent = data.Skip(index).Take(infoLength).ToArray();
                index += infoLength;

                switch (infoId)
                {
                    case 0x01:
                        gpsData.Mileage = ReadUInt32BigEndian(infoContent, 0) / 10.0;
                        break;
                    case 0x25:
                        gpsData.ExtendedStatus = ReadUInt32BigEndian(infoContent, 0);
                        break;
                    case 0x2A:
                        gpsData.IOStatus = ReadUInt16BigEndian(infoContent, 0);
                        break;
                    case 0x30:
                        gpsData.NetworkSignal = infoContent[0];
                        break;
                    case 0x31:
                        gpsData.Satellites = infoContent[0];
                        break;
                    case 0xE3:
                        gpsData.BatteryVoltage = ReadUInt16BigEndian(infoContent, 0) * 0.001;
                        break;
                    default:
                        break;
                }
            }
        }

        private void HandleTextMessage(string deviceId, byte[] msgBody, GpsData gpsData)
        {
            var message = Encoding.GetEncoding("GBK").GetString(msgBody);

            gpsData.RequestBody = "Handle text message " + gpsData.RequestBody;
            _gpsWriteRequestLogSingleton.Write(gpsData);
        }

        private void HandleControlResponse(string deviceId, byte[] msgBody, GpsData gpsData)
        {
            gpsData.RequestBody = "Handle control response " + gpsData.RequestBody;
        }

        private byte[] HandleTimeSyncRequest(ushort msgSerialNumber, string deviceId, GpsData gpsData)
        {
            ushort responseMsgId = 0x8F01;
            var responseBody = new List<byte>();

            responseBody.Add(0x01); // Result: success
            var time = DateTime.UtcNow.ToString("yyMMddHHmmss");
            var timeBytes = StringToBCD(time, 6);
            responseBody.AddRange(timeBytes);

            var responseData = BuildJT808Message(responseMsgId, deviceId, responseBody.ToArray());

            gpsData.RequestBody = "Handle time sync request" + gpsData.RequestBody;
            _gpsWriteRequestLogSingleton.Write(gpsData);
            return responseData;
        }

        private byte[] CreateUniversalResponse(ushort msgSerialNumber, ushort msgId, byte result, string deviceId)
        {
            ushort responseMsgId = 0x8001;
            var responseBody = new List<byte>();

            responseBody.Add((byte)(msgSerialNumber >> 8));
            responseBody.Add((byte)(msgSerialNumber & 0xFF));
            responseBody.Add((byte)(msgId >> 8));
            responseBody.Add((byte)(msgId & 0xFF));
            responseBody.Add(result);

            var responseData = BuildJT808Message(responseMsgId, deviceId, responseBody.ToArray());
            return responseData;
        }

        private byte[] BuildJT808Message(ushort msgId, string deviceId, byte[] msgBody)
        {
            var message = new List<byte>();

            message.Add((byte)(msgId >> 8));
            message.Add((byte)(msgId & 0xFF));

            ushort bodyProps = (ushort)msgBody.Length;
            message.Add((byte)(bodyProps >> 8));
            message.Add((byte)(bodyProps & 0xFF));

            var deviceIdBCD = StringToBCD(deviceId, 6);
            message.AddRange(deviceIdBCD);

            ushort msgSerialNumber = GetNextSerialNumber();
            message.Add((byte)(msgSerialNumber >> 8));
            message.Add((byte)(msgSerialNumber & 0xFF));

            message.AddRange(msgBody);

            byte checksum = 0;
            foreach (var b in message)
            {
                checksum ^= b;
            }

            var fullMessage = new List<byte> { 0x7E };
            var escapedData = Escape(message.ToArray());
            fullMessage.AddRange(escapedData);
            fullMessage.Add(checksum);
            fullMessage.Add(0x7E);

            return fullMessage.ToArray();
        }

        private ushort GetNextSerialNumber()
        {
            _serialNumber++;
            if (_serialNumber == 0)
                _serialNumber = 1;
            return _serialNumber;
        }

        private string BCDToString(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder();
            for (int i = offset; i < offset + length; i++)
            {
                sb.Append((data[i] >> 4).ToString("X"));
                sb.Append((data[i] & 0x0F).ToString("X"));
            }
            return sb.ToString().TrimStart('0');
        }

        private byte[] StringToBCD(string str, int length)
        {
            if (str.Length % 2 != 0)
                str = "0" + str;

            var bcd = new byte[length];
            int strIndex = 0;

            for (int i = 0; i < length; i++)
            {
                byte highNibble = 0;
                byte lowNibble = 0;

                if (strIndex < str.Length)
                    highNibble = (byte)(str[strIndex++] - '0');
                if (strIndex < str.Length)
                    lowNibble = (byte)(str[strIndex++] - '0');

                bcd[i] = (byte)((highNibble << 4) | lowNibble);
            }
            return bcd;
        }

        private uint ReadUInt32BigEndian(byte[] buffer, int offset)
        {
            return (uint)(
                (buffer[offset] << 24) |
                (buffer[offset + 1] << 16) |
                (buffer[offset + 2] << 8) |
                buffer[offset + 3]
            );
        }

        private ushort ReadUInt16BigEndian(byte[] buffer, int offset)
        {
            return (ushort)(
                (buffer[offset] << 8) |
                buffer[offset + 1]
            );
        }
    }
}
