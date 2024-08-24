using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Diagnostics;

class Program
{
    private static readonly int size = 1000;
    private static readonly bool debug = false;

    static async Task Main(string[] args)
    {
        // Create a new instance of HttpClient
        using (HttpClient client = new HttpClient())
        {
            try
            {
                await InitializeAsync();

                // Start timer
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                
                int[,] A = new int[size, size];
                int[,] B = new int[size, size];

                await RequestMatrices(A, B);
                
                // debug
                if (debug) PrintTable("A", A);
                if (debug) PrintTable("B", B);

                // Multiply A x B
                StringBuilder sb = new StringBuilder(size);
                int[,] C = Multiply(A, B, sb);

                // debug
                if (debug) PrintTable("C", C);                

                StringBuilder answer = GenerateMD5(sb);

                stopwatch.Stop();
                TimeSpan elapsedTime = stopwatch.Elapsed;
                Console.WriteLine($"Execution Time: {elapsedTime.TotalSeconds} milliseconds.  Sending {answer}...");

                // POST result
                await PostResults(answer);
                
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
            }
        }
    }

    static async Task InitializeAsync()
    {
        using (HttpClient client = new HttpClient())
        {
            HttpResponseMessage response = await client.GetAsync($"https://recruitment-test.investcloud.com/api/numbers/init/{size}");
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            if (debug) Console.WriteLine(responseBody);
        }
    }

    static async Task RequestMatrices(int[,] A, int[,] B)
    {
        var requestSets = new Dictionary<string, string[]>
                {
                    { "A", GenerateUrls(size, "A") },
                    { "B", GenerateUrls(size, "B") }
                };

        using (HttpClient client = new HttpClient())
        {
            foreach (var set in requestSets)
            {
                string setIdentifier = set.Key;
                string[] urls = set.Value;

                // Create a list of tasks with their indexes and identifiers
                var tasks = new Task<RequestResult>[urls.Length];

                // Start all the requests in parallel with their index and set identifier
                for (int i = 0; i < size; i++)
                {
                    //int index = i; // Capture the current index
                    tasks[i] = MakeRequestAsync(client, setIdentifier, i, urls[i]);
                }


                // Await all tasks to complete
                var results = await Task.WhenAll(tasks);

                // Process each response and assign to corresponding matrix 
                foreach (var result in results)
                {
                    string identifier = result.SetIdentifier;
                    int index = result.Index;
                    HttpResponseMessage resp = result.Response;

                    // Ensure the request was successful
                    resp.EnsureSuccessStatusCode();

                    // Read the response content
                    string rb = await resp.Content.ReadAsStringAsync();
                    var json = JsonSerializer.Deserialize<Result>(rb);
                    if (debug) Console.WriteLine($"Set: {identifier}, Index: {index}, Response: {rb}");

                    // copy response into matrices
                    for (int col = 0; col < size; col++)
                    {
                        if (setIdentifier == "A")
                        {
                            A[index, col] = json.Value[col];
                        }
                        else
                        {
                            B[index, col] = json.Value[col];
                        }

                    }
                }
            }
        }
    }

    static async Task PostResults(StringBuilder answer)
    {
        using (HttpClient client = new HttpClient())
        {
            HttpContent content = new StringContent(answer.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage responsePost = await client.PostAsync("https://recruitment-test.investcloud.com/api/numbers/validate", content);
            responsePost.EnsureSuccessStatusCode();
            string responsePostString = await responsePost.Content.ReadAsStringAsync();
            Console.WriteLine("Response:" + responsePostString);
        }
    }

    static string[] GenerateUrls(int count, string set)
    {
        var urls = new string[count];
        for (int i = 0; i < count; i++)
        {
            urls[i] = $"https://recruitment-test.investcloud.com/api/numbers/{set}/row/{i}";            
        }
        return urls;
    }


    static async Task<RequestResult> MakeRequestAsync(HttpClient client, string setIdentifier, int index, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url);
        return new RequestResult { SetIdentifier = setIdentifier, Index = index, Response = response };
    }

    static StringBuilder GenerateMD5(StringBuilder tableString)
    {
        StringBuilder answer = new StringBuilder();
        using (MD5 md5 = MD5.Create())
        {
            byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(tableString.ToString()));
            for (int i = 0; i < data.Length; i++)
            {
                answer.Append(data[i].ToString("x2"));
            }

        }
        if (debug) Console.WriteLine($"MD5 Result {answer.ToString()}.");

        return answer;
    }

    static void PrintTable(string tableId, int[,] table)
    {
        Console.WriteLine(tableId);
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                Console.Write($"{String.Join(", ", table[i, j])}  ");
            }
            Console.WriteLine();
        }
    }

    static int[,] Multiply(int[,] A, int[,] B, StringBuilder sb = null)
    {
        int[,] C = new int[size, size]; 
        //StringBuilder sb = new StringBuilder(size);

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                C[i, j] = 0;
                for (int k = 0; k < size; k++)
                {
                    C[i, j] += A[i, k] * B[k, j];
                }
                if (sb != null)
                {
                    sb.Append(Math.Abs(C[i, j])); // Not sure if the string has to be of the absolute values 
                }
            }
        }
        return C;
    }
}

// Supporting classes 

// Result from each request 
public class Result
{
    public int[] Value { get; set; }
    public string Cause { get; set; }
    public bool Success { get; set; }
}

// Mapping of the request to its corresponding matrix row
class RequestResult
{
    public string SetIdentifier { get; set; }
    public int Index { get; set; }
    public HttpResponseMessage Response { get; set; }
}