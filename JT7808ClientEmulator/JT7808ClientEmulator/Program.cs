using System.Net.Sockets;
using System.Text;

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

                // Skonstruuj wiadomość autoryzacyjną
                byte[] authMessage = BuildAuthenticationMessage();

                // Wyślij wiadomość do serwera
                stream.Write(authMessage, 0, authMessage.Length);
                Console.WriteLine("Wysłano wiadomość autoryzacyjną.");

                // Odbierz odpowiedź z serwera
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                Console.WriteLine("Otrzymano odpowiedź od serwera:");

                // Wyświetl odpowiedź w formacie heksadecymalnym
                Console.WriteLine(BitConverter.ToString(buffer, 0, bytesRead));

                // Zamknij połączenie
                stream.Close();
                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd: " + ex.Message);
            }

            Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
            Console.ReadKey();
        }

        static byte[] BuildAuthenticationMessage()
        {
            // Konstruuj wiadomość autoryzacyjną zgodnie z protokołem JT808
            // Message ID: 0x0102 (Authentication request)
            ushort msgId = 0x0102;

            // Message Body
            string authCode = "AUTH_CODE"; // Kod autoryzacyjny
            byte[] authCodeBytes = Encoding.ASCII.GetBytes(authCode);

            // Device ID (BCD[6])
            string deviceId = "123456789012"; // Przykładowy ID urządzenia (12 cyfr)
            byte[] deviceIdBCD = StringToBCD(deviceId);

            // Serial Number
            ushort msgSerialNumber = 1;

            // Message Body Properties
            ushort msgBodyProps = (ushort)authCodeBytes.Length;

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
            message.AddRange(authCodeBytes);

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
            int val = hex - '0';
            if (val > 9)
            {
                val = hex - 'A' + 10;
            }
            return val;
        }
    }
}
