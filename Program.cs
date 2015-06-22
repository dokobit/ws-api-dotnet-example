using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace iSignNetExample
{
    class Program
    {
        public class Api
        {
            public static string accessToken = ""; //Enter Your iSign.io API access token
        }

        [DataContract]
        public class Response
        {
            [DataMember(Name="status")]
            public string Status { get; set; }
            [DataMember(IsRequired = false, Name="controlCode")]
            public string ControlCode { get; set; }
            [DataMember(IsRequired = false, Name = "token")]
            public string Token { get; set; }
            [DataMember(IsRequired = false, Name = "message")]
            public string Message { get; set; }
            [DataMember(IsRequired = false, Name = "errors")]
            public IEnumerable<string> Errors { get; set; }
        }

        [DataContract]
        public class FileResponse
        {
            [DataMember(Name = "status")]
            public string Status { get; set; }
            [DataMember(IsRequired = false, Name = "signature_id")]
            public string SignatureId { get; set; }
            [DataMember(IsRequired = false, Name = "file")]
            public File File { get; set; }
        }

        [DataContract]
        public class File
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }
            [DataMember(Name = "content")]
            public string Content { get; set; }
            [DataMember(Name = "digest")]
            public string Digest { get; set; }
        }

        public static Response Sign(byte[] document, string phone, string code)
        {
            using (var client = new HttpClient())
            {
                using (var content =
                    new MultipartFormDataContent("Upload----" + DateTime.Now))
                {
                    content.Add(new StringContent("pdf"), "type");
                    content.Add(new StringContent(phone), "phone");
                    content.Add(new StringContent(code), "code");
                    content.Add(new StringContent("true"), "timestamp");
                    content.Add(new StringContent("Vardas Pavardenis"), "pdf[contact]");
                    content.Add(new StringContent("Test"), "pdf[reason]");
                    content.Add(new StringContent("Vilnius"), "pdf[location]");
                    content.Add(new StringContent("test.pdf"), "pdf[files][0][name]");
                    content.Add(new StringContent(Convert.ToBase64String(document)), "pdf[files][0][content]");
                    content.Add(
                        new StringContent(
                            BitConverter.ToString(SHA1.Create().ComputeHash(document)).Replace("-", "").ToLower()),
                        "pdf[files][0][digest]");
                    using (
                        var message =
                            client.PostAsync("https://developers.isign.io/mobile/sign.json?access_token="+Api.accessToken,
                                content))
                    {
                        var input = message.Result;
                        var serializator = new DataContractJsonSerializer(typeof (Response));
                        return (Response) serializator.ReadObject(input.Content.ReadAsStreamAsync().Result);
                    }
                }
            }
        }

        public static FileResponse GetDocument(Response response)
        {
            using (var client = new HttpClient())
            {
                using (var message = client.GetAsync(string.Format("https://developers.isign.io/mobile/sign/status/{0}.json?access_token="+Api.accessToken, response.Token)))
                {
                    var input = message.Result;
                    var serializator = new DataContractJsonSerializer(typeof(FileResponse));
                    return (FileResponse)serializator.ReadObject(input.Content.ReadAsStreamAsync().Result);
                }
            }
        }

        static void Main(string[] args)
        {
            string fileName = args.Length > 0 ? args[0] : @"../../test.pdf"; // example pdf file to sign
            string phone = args.Length > 1 ? args[1] : "+37060000007"; // enter phone with country code
            string code = args.Length > 2 ? args[2] : "51001091072"; // enter personal code
            
            byte[] contentData = System.IO.File.ReadAllBytes(fileName);
            var response = Sign(contentData, phone, code);
            if (response.Status == "ok")
            {
                Console.WriteLine("iSign.io API signing example. You will receive:\nControl code: {0}, for signign token: {1}", response.ControlCode, response.Token);
                FileResponse fileResponse = null;
                //Thread.Sleep(30000);
                for (int i = 0; i < 30; i++)
                {
                    Console.WriteLine("Waiting");
                    fileResponse = GetDocument(response);
                    if (fileResponse.Status != "waiting") break;
                    Thread.Sleep(1000);
                }

                if (fileResponse == null || fileResponse.Status != "ok")
                {
                    Console.WriteLine("Failed to receive response or response is not ok");
                    return;
                }

                if (fileResponse.File != null)
                {
                    System.IO.File.WriteAllBytes("test-result.pdf", Convert.FromBase64String(fileResponse.File.Content));
                    Console.WriteLine("Received response. Open ./test-result.pdf");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
            else
            {
                Console.WriteLine("Status: " + response.Status + "\nMessage: " + response.Message);
                if (response.Errors != null && response.Errors.Count() > 0) {
                    Console.WriteLine("\nErrors:");
                    foreach (var error in response.Errors)
                    {
                        Console.WriteLine("\n " + error);
                    }
                }
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
        }
    }
}
