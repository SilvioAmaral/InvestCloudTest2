using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Diagnostics;

public class Result
{
    public int[] Value { get; set; }
    public string Cause { get; set; }
    public bool Success { get; set; }
}

class Program
{
    static async Task Main(string[] args)
    {
        // Create a new instance of HttpClient
        using (HttpClient client = new HttpClient())
        {
            try
            {
                int size = 3;
                HttpResponseMessage response = await client.GetAsync($"https://recruitment-test.investcloud.com/api/numbers/init/{size}");
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                int[,] A = new int[3, 3];
                int[,] B = new int[3, 3];

                for (int i = 0; i < size; i++)
                {
                    HttpResponseMessage responseA = await client.GetAsync($"https://recruitment-test.investcloud.com/api/numbers/A/row/{i}");
                    string responseBodyA = await responseA.Content.ReadAsStringAsync();
                    var resultA = JsonSerializer.Deserialize<Result>(responseBodyA);
                    Console.WriteLine(String.Join(", ", resultA.Value));
                    for (int col = 0; col < size; col++)
                    {
                        A[i, col] = resultA.Value[col];
                    }

                    HttpResponseMessage responseB = await client.GetAsync($"https://recruitment-test.investcloud.com/api/numbers/B/col/{i}");
                    string responseBodyB = await responseB.Content.ReadAsStringAsync();
                    var resultB = JsonSerializer.Deserialize<Result>(responseBodyB);
                    Console.WriteLine(String.Join(", ", resultB.Value));
                    for (int row = 0; row < size; row++)
                    {
                        B[row, i] = resultB.Value[row];
                    }

                }

                // For debugging purposes
                //for (int i = 0; i < size; i++)
                //{
                //    for (int j = 0; j < size; j++)
                //    {
                //        Console.WriteLine($"A - [i:{i}, j:{j}] - {String.Join(", ", A[i, j])}");
                //    }
                //}
                //for (int i = 0; i < size; i++)
                //{
                //    for (int j = 0; j < size; j++)
                //    {
                //        Console.WriteLine($"B - [i:{i}, j:{j}] - {String.Join(", ", B[i, j])}");
                //    }
                //}

                int[,] C = new int[size, size]; //result 
                StringBuilder sb = new StringBuilder(size);

                for (int i = 0; i < size; i++)
                {
                    for (int j = 0; j < size; j++)
                    {
                        C[i, j] = 0;
                        for (int k = 0; k < size; k++)
                        {
                            C[i, j] += A[i, k] * B[k, j];
                        }
                        sb.Append(C[i, j]);
                    }
                }
                Console.WriteLine($"Concatenated MAtrix C: {sb.ToString()}");
                
                StringBuilder result = new StringBuilder();
                using (MD5 md5 = MD5.Create())
                {
                    byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                    for (int i = 0; i < data.Length; i++)
                    {
                        result.Append(data[i].ToString("x2"));
                    }

                }

                stopwatch.Stop();
                TimeSpan elapsedTime = stopwatch.Elapsed;
                Console.WriteLine($"MD5 Result {result.ToString()}");
                Console.WriteLine($"Execution Time: {elapsedTime.TotalMilliseconds} milliseconds");

                // POST 
                HttpContent content = new StringContent(result.ToString(), Encoding.UTF8, "text/json");
                HttpResponseMessage responsePost = await client.PostAsync("https://recruitment-test.investcloud.com/api/numbers/validate", content);
                response.EnsureSuccessStatusCode();
                string responsePostString = await responsePost.Content.ReadAsStringAsync();
                Console.WriteLine("Response:"+responsePostString);

            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
            }
        }
    }
}
