using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace JT7808ClientEmulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string serverIp = "127.0.0.1"; // Adres IP Twojego serwera
            int serverPort = 2323; // Port, na którym nasłuchuje Twój serwer JT808

            try
            {
                TcpClient client = new TcpClient();
                client.Connect(serverIp, serverPort);
                Console.WriteLine("Połączono z serwerem.");

                NetworkStream stream = client.GetStream();

                // 1. Wyślij wiadomość rejestracji
                byte[] registrationMessage = BuildRegistrationMessage();
                SendMessage(stream, registrationMessage, "rejestracji");

                // 2. Wyślij wiadomość autoryzacyjną
                byte[] authMessage = BuildAuthenticationMessage();
                SendMessage(stream, authMessage, "autoryzacyjną");

                // 3. Wyślij raport lokalizacji
                byte[] locationMessage = BuildLocationReportMessage();
                SendMessage(stream, locationMessage, "raportu lokalizacji");

                // 4. Wyślij wiadomość heartbeat
                byte[] heartbeatMessage = BuildHeartbeatMessage();
                SendMessage(stream, heartbeatMessage, "heartbeat");

                // 5. Wyślij wiadomość logout
                byte[] logoutMessage = BuildLogoutMessage();
                SendMessage(stream, logoutMessage, "logout");

                // Zamknij połączenie
                stream.Close();
                client.Close();
                Console.WriteLine("Połączenie zamknięte.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd: " + ex.Message);
            }

            Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
            Console.ReadKey();
        }

        static void SendMessage(NetworkStream stream, byte[] message, string messageType)
        {
            // Wyślij wiadomość do serwera
            stream.Write(message, 0, message.Length);
            Console.WriteLine($"Wysłano wiadomość {messageType}.");

            // Odbierz odpowiedź z serwera
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            Console.WriteLine("Otrzymano odpowiedź od serwera:");

            // Wyświetl odpowiedź w formacie heksadecymalnym
            Console.WriteLine(BitConverter.ToString(buffer, 0, bytesRead));

            // Opcjonalnie: Dodaj opóźnienie między wiadomościami
            Thread.Sleep(1000);
        }

        static byte[] BuildRegistrationMessage()
        {
            // Konstruuj wiadomość rejestracji zgodnie z protokołem JT808
            // Message ID: 0x0100 (Registration request)
            ushort msgId = 0x0100;

            // Message Body
            List<byte> body = new List<byte>();

            // Province ID (2 bytes)
            ushort provinceId = 0x0033;
            body.Add((byte)(provinceId >> 8));
            body.Add((byte)(provinceId & 0xFF));

            // City ID (2 bytes)
            ushort cityId = 0x0044;
            body.Add((byte)(cityId >> 8));
            body.Add((byte)(cityId & 0xFF));

            // Manufacturer ID (5 bytes)
            string manufacturerId = "ABCDE";
            byte[] manufacturerIdBytes = Encoding.ASCII.GetBytes(manufacturerId.PadRight(5, '\0'));
            body.AddRange(manufacturerIdBytes);

            // Terminal Model (20 bytes)
            string terminalModel = "JT808Client";
            byte[] terminalModelBytes = Encoding.ASCII.GetBytes(terminalModel.PadRight(20, '\0'));
            body.AddRange(terminalModelBytes);

            // Terminal ID (7 bytes)
            string terminalId = "1234567";
            byte[] terminalIdBytes = Encoding.ASCII.GetBytes(terminalId.PadRight(7, '\0'));
            body.AddRange(terminalIdBytes);

            // License Plate Color (1 byte)
            byte plateColor = 0x01; // Blue
            body.Add(plateColor);

            // License Plate (variable, GBK encoding)
            string plateNumber = "W12345";
            byte[] plateNumberBytes = Encoding.GetEncoding("GBK").GetBytes(plateNumber);
            body.AddRange(plateNumberBytes);

            byte[] bodyBytes = body.ToArray();

            // Device ID (BCD[6])
            string deviceId = "123456789012"; // Przykładowy ID urządzenia (12 cyfr)
            byte[] deviceIdBCD = StringToBCD(deviceId);

            // Serial Number
            ushort msgSerialNumber = 1;

            // Message Body Properties
            ushort msgBodyProps = (ushort)bodyBytes.Length;

            // Buduj wiadomość
            List<byte> message = new List<byte>();

            // Dodaj start flagę
            message.Add(0x7E);

            // Dodaj Message Header
            message.Add((byte)(msgId >> 8));
            message.Add((byte)(msgId & 0xFF));
            message.Add((byte)(msgBodyProps >> 8));
            message.Add((byte)(msgBodyProps & 0xFF));
            message.AddRange(deviceIdBCD);
            message.Add((byte)(msgSerialNumber >> 8));
            message.Add((byte)(msgSerialNumber & 0xFF));

            // Dodaj Message Body
            message.AddRange(bodyBytes);

            // Oblicz sumę kontrolną
            byte checksum = 0;
            for (int i = 1; i < message.Count; i++)
            {
                checksum ^= message[i];
            }

            // Dodaj sumę kontrolną
            message.Add(checksum);

            // Dodaj end flagę
            message.Add(0x7E);

            // Zwróć wiadomość jako tablicę bajtów
            return message.ToArray();
        }

        static byte[] BuildAuthenticationMessage()
        {
            // Konstruuj wiadomość autoryzacyjną zgodnie z protokołem JT808
            // Message ID: 0x0102 (Authentication request)
            ushort msgId = 0x0102;

            // Message Body
            string authCode = "AUTH_CODE"; // Kod autoryzacyjny
            byte[] authCodeBytes = Encoding.ASCII.GetBytes(authCode);

            byte[] bodyBytes = authCodeBytes;

            // Device ID (BCD[6])
            string deviceId = "123456789012"; // Przykładowy ID urządzenia (12 cyfr)
            byte[] deviceIdBCD = StringToBCD(deviceId);

            // Serial Number
            ushort msgSerialNumber = 2;

            // Message Body Properties
            ushort msgBodyProps = (ushort)bodyBytes.Length;

            // Buduj wiadomość
            List<byte> message = new List<byte>();

            // Dodaj start flagę
            message.Add(0x7E);

            // Dodaj Message Header
            message.Add((byte)(msgId >> 8));
            message.Add((byte)(msgId & 0xFF));
            message.Add((byte)(msgBodyProps >> 8));
            message.Add((byte)(msgBodyProps & 0xFF));
            message.AddRange(deviceIdBCD);
            message.Add((byte)(msgSerialNumber >> 8));
            message.Add((byte)(msgSerialNumber & 0xFF));

            // Dodaj Message Body
            message.AddRange(bodyBytes);

            // Oblicz sumę kontrolną
            byte checksum = 0;
            for (int i = 1; i < message.Count; i++)
            {
                checksum ^= message[i];
            }

            // Dodaj sumę kontrolną
            message.Add(checksum);

            // Dodaj end flagę
            message.Add(0x7E);

            // Zwróć wiadomość jako tablicę bajtów
            return message.ToArray();
        }

        static byte[] BuildLocationReportMessage()
        {
            // Konstruuj wiadomość raportu lokalizacji zgodnie z protokołem JT808
            // Message ID: 0x0200 (Location report)
            ushort msgId = 0x0200;

            // Message Body
            List<byte> body = new List<byte>();

            // Alarm flags (4 bytes)
            uint alarmFlags = 0x00000000;
            body.AddRange(BitConverter.GetBytes(alarmFlags).Reverse());

            // Status flags (4 bytes)
            uint statusFlags = 0x00000000;
            body.AddRange(BitConverter.GetBytes(statusFlags).Reverse());

            // Latitude (4 bytes)
            uint latitude = (uint)(22.543096 * 1000000); // Przykładowa szerokość geograficzna
            body.AddRange(BitConverter.GetBytes(latitude).Reverse());

            // Longitude (4 bytes)
            uint longitude = (uint)(114.057865 * 1000000); // Przykładowa długość geograficzna
            body.AddRange(BitConverter.GetBytes(longitude).Reverse());

            // Altitude (2 bytes)
            ushort altitude = 10; // W metrach
            body.Add((byte)(altitude >> 8));
            body.Add((byte)(altitude & 0xFF));

            // Speed (2 bytes)
            ushort speed = 60; // W km/h
            body.Add((byte)(speed >> 8));
            body.Add((byte)(speed & 0xFF));

            // Direction (2 bytes)
            ushort direction = 90; // W stopniach
            body.Add((byte)(direction >> 8));
            body.Add((byte)(direction & 0xFF));

            // Time (6 bytes), BCD[6], YYMMDDhhmmss
            string time = DateTime.Now.ToString("yyMMddHHmmss");
            byte[] timeBytes = StringToBCD(time);
            body.AddRange(timeBytes);

            byte[] bodyBytes = body.ToArray();

            // Device ID (BCD[6])
            string deviceId = "123456789012";
            byte[] deviceIdBCD = StringToBCD(deviceId);

            // Serial Number
            ushort msgSerialNumber = 3;

            // Message Body Properties
            ushort msgBodyProps = (ushort)bodyBytes.Length;

            // Buduj wiadomość
            List<byte> message = new List<byte>();

            // Dodaj start flagę
            message.Add(0x7E);

            // Dodaj Message Header
            message.Add((byte)(msgId >> 8));
            message.Add((byte)(msgId & 0xFF));
            message.Add((byte)(msgBodyProps >> 8));
            message.Add((byte)(msgBodyProps & 0xFF));
            message.AddRange(deviceIdBCD);
            message.Add((byte)(msgSerialNumber >> 8));
            message.Add((byte)(msgSerialNumber & 0xFF));

            // Dodaj Message Body
            message.AddRange(bodyBytes);

            // Oblicz sumę kontrolną
            byte checksum = 0;
            for (int i = 1; i < message.Count; i++)
            {
                checksum ^= message[i];
            }

            // Dodaj sumę kontrolną
            message.Add(checksum);

            // Dodaj end flagę
            message.Add(0x7E);

            // Zwróć wiadomość jako tablicę bajtów
            return message.ToArray();
        }

        static byte[] BuildHeartbeatMessage()
        {
            // Konstruuj wiadomość heartbeat zgodnie z protokołem JT808
            // Message ID: 0x0002 (Heartbeat)
            ushort msgId = 0x0002;

            // Message Body (brak ciała w heartbeat)
            byte[] bodyBytes = new byte[0];

            // Device ID (BCD[6])
            string deviceId = "123456789012";
            byte[] deviceIdBCD = StringToBCD(deviceId);

            // Serial Number
            ushort msgSerialNumber = 4;

            // Message Body Properties
            ushort msgBodyProps = (ushort)bodyBytes.Length;

            // Buduj wiadomość
            List<byte> message = new List<byte>();

            // Dodaj start flagę
            message.Add(0x7E);

            // Dodaj Message Header
            message.Add((byte)(msgId >> 8));
            message.Add((byte)(msgId & 0xFF));
            message.Add((byte)(msgBodyProps >> 8));
            message.Add((byte)(msgBodyProps & 0xFF));
            message.AddRange(deviceIdBCD);
            message.Add((byte)(msgSerialNumber >> 8));
            message.Add((byte)(msgSerialNumber & 0xFF));

            // Brak Message Body

            // Oblicz sumę kontrolną
            byte checksum = 0;
            for (int i = 1; i < message.Count; i++)
            {
                checksum ^= message[i];
            }

            // Dodaj sumę kontrolną
            message.Add(checksum);

            // Dodaj end flagę
            message.Add(0x7E);

            return message.ToArray();
        }

        static byte[] BuildLogoutMessage()
        {
            // Konstruuj wiadomość logout zgodnie z protokołem JT808
            // Message ID: 0x0003 (Terminal Logout)
            ushort msgId = 0x0003;

            // Message Body (brak ciała w logout)
            byte[] bodyBytes = new byte[0];

            // Device ID (BCD[6])
            string deviceId = "123456789012";
            byte[] deviceIdBCD = StringToBCD(deviceId);

            // Serial Number
            ushort msgSerialNumber = 5;

            // Message Body Properties
            ushort msgBodyProps = (ushort)bodyBytes.Length;

            // Buduj wiadomość
            List<byte> message = new List<byte>();

            // Dodaj start flagę
            message.Add(0x7E);

            // Dodaj Message Header
            message.Add((byte)(msgId >> 8));
            message.Add((byte)(msgId & 0xFF));
            message.Add((byte)(msgBodyProps >> 8));
            message.Add((byte)(msgBodyProps & 0xFF));
            message.AddRange(deviceIdBCD);
            message.Add((byte)(msgSerialNumber >> 8));
            message.Add((byte)(msgSerialNumber & 0xFF));

            // Brak Message Body

            // Oblicz sumę kontrolną
            byte checksum = 0;
            for (int i = 1; i < message.Count; i++)
            {
                checksum ^= message[i];
            }

            // Dodaj sumę kontrolną
            message.Add(checksum);

            // Dodaj end flagę
            message.Add(0x7E);

            return message.ToArray();
        }

        static byte[] StringToBCD(string str)
        {
            if (str.Length % 2 != 0)
            {
                str = "0" + str;
            }

            byte[] bcd = new byte[str.Length / 2];
            for (int i = 0; i < bcd.Length; i++)
            {
                bcd[i] = (byte)(((GetHexValue(str[2 * i]) << 4) | GetHexValue(str[2 * i + 1])));
            }
            return bcd;
        }

        static int GetHexValue(char hex)
        {
            if (hex >= '0' && hex <= '9')
                return hex - '0';
            else if (hex >= 'A' && hex <= 'F')
                return hex - 'A' + 10;
            else if (hex >= 'a' && hex <= 'f')
                return hex - 'a' + 10;
            else
                throw new ArgumentException("Nieprawidłowy znak heksadecymalny");
        }
    }
}
