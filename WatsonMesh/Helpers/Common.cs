using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Watson
{
    /// <summary>
    /// Commonly-used static methods.
    /// </summary>
    internal static class Common
    {
        public static string SerializeJson(object obj, bool pretty)
        {
	        return obj is null 
		        ? null 
		        : JsonConvert.SerializeObject(obj, new JsonSerializerSettings
			        {
				        NullValueHandling = NullValueHandling.Ignore, ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
				        DateFormatHandling = DateFormatHandling.IsoDateFormat,
				        Formatting = pretty ? Formatting.Indented : Formatting.None
			        });
		}

        public static T DeserializeJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
	            throw new ArgumentNullException(nameof(json));

			return JsonConvert.DeserializeObject<T>(json);
		}

        public static T DeserializeJson<T>(byte[] data)
        {
            if (data == null || data.Length < 1)
	            throw new ArgumentNullException(nameof(data));

            return DeserializeJson<T>(Encoding.UTF8.GetString(data));
        }

        public static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault)
	            Console.Write(" [Y/n]? ");
            else
	            Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (string.IsNullOrEmpty(userInput))
            {
	            return yesDefault;
            }
            userInput = userInput.ToLower();

	        return yesDefault ?
				(string.Compare(userInput, "n") == 0) || (string.Compare(userInput, "no") == 0)
				: (string.Compare(userInput, "y") == 0) || (string.Compare(userInput, "yes") == 0);
        }

        public static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!string.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                var userInput = Console.ReadLine();

                if (string.IsNullOrEmpty(userInput))
                {
                    if (!string.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }

        public static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

	            var userInput = Console.ReadLine();

                if (string.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                if (!int.TryParse(userInput, out var ret))
                {
                    Console.WriteLine("Please enter a valid integer.");
                    continue;
                }

                if (ret == 0 && allowZero)
                {
					return 0;
				}

                if (ret < 0 && positiveOnly)
                {
					Console.WriteLine("Please enter a value greater than zero.");
	                continue;
				}

                return ret;
            }
        }

        public static string Md5(byte[] data)
        {
            if (data == null)
	            return null;

            var md5 = MD5.Create();
	        var hash = md5.ComputeHash(data);
	        var sb = new StringBuilder();

	        for (int i = 0; i < hash.Length; i++)
	        {
				sb.Append(hash[i].ToString("X2"));
	        }

            return sb.ToString();
        }

        public static string Md5(string data)
        {
            if (string.IsNullOrEmpty(data))
	            return null;

	        var md5 = MD5.Create();
	        var dataBytes = Encoding.ASCII.GetBytes(data);
	        var hash = md5.ComputeHash(dataBytes);
	        var sb = new StringBuilder();

	        for (int i = 0; i < hash.Length; i++)
	        {
		        sb.Append(hash[i].ToString("X2"));
			}
            
            return sb.ToString();
		}

        public static byte[] Sha1(byte[] data)
        {
            if (data == null || data.Length < 1)
	            return null;

            var s = new SHA1Managed();

            return s.ComputeHash(data);
        }

        public static byte[] Sha256(byte[] data)
        {
            if (data == null || data.Length < 1)
	            return null;

	        var s = new SHA256Managed();

            return s.ComputeHash(data);
        }

        public static byte[] AppendBytes(byte[] head, byte[] tail)
        {
            byte[] ret;

            if (head == null || head.Length == 0)
            {
                if (tail == null || tail.Length == 0)
	                return null;

                ret = new byte[tail.Length];

                Buffer.BlockCopy(tail, 0, ret, 0, tail.Length);

                return ret;
            }
            else
            {
                if (tail == null || tail.Length == 0)
	                return head;

                ret = new byte[head.Length + tail.Length];

                Buffer.BlockCopy(head, 0, ret, 0, head.Length);
                Buffer.BlockCopy(tail, 0, ret, head.Length, tail.Length);

                return ret;
            }
        }

        public static byte[] ReadStream(long contentLength, Stream stream)
        {
            if (contentLength < 1)
	            throw new ArgumentException("Content length must be greater than zero.");

            if (stream == null)
	            throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
	            throw new ArgumentException("Cannot read from supplied stream.");

	        var bytesRead = 0;
	        var bytesRemaining = contentLength;
	        var buffer = new byte[65536];
            byte[] ret = null;

            while (bytesRemaining > 0)
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    if (bytesRead == buffer.Length)
                    {
                        ret = AppendBytes(ret, buffer);
                    }
                    else
                    {
                        byte[] temp = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                        ret = AppendBytes(ret, temp);
                    } 
                    bytesRemaining -= bytesRead;
                }
            }

            return ret;
        }

        public static byte[] ReadStream(Stream stream)
        {
            if (stream == null)
	            throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
	            throw new ArgumentException("Cannot read from supplied stream.");

	        var bytesRead = 0;
	        var buffer = new byte[65536];
            byte[] ret = null;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (bytesRead == buffer.Length)
                {
                    ret = AppendBytes(ret, buffer);
                }
                else
                {
                    byte[] temp = new byte[bytesRead];

                    Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                    ret = AppendBytes(ret, buffer);
                }
            }

            return ret;
        }

        public static void LogException(Exception e)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine(" = Exception Type: " + e.GetType().ToString());
            Console.WriteLine(" = Exception Data: " + e.Data);
            Console.WriteLine(" = Inner Exception: " + e.InnerException);
            Console.WriteLine(" = Exception Message: " + e.Message);
            Console.WriteLine(" = Exception Source: " + e.Source);
            Console.WriteLine(" = Exception StackTrace: " + e.StackTrace);
            Console.WriteLine("================================================================================");
        }
    }
}
