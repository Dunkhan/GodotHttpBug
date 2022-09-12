using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public class Test2 : Control
{
    private RichTextLabel label;
    private HTTPRequest httpRequest;

    public override void _Ready()
    {
        GetNode("HTTPRequest").Connect("request_completed", this, "OnRequestCompleted");
        label = GetNode<RichTextLabel>("RichTextLabel");
        httpRequest = GetNode<HTTPRequest>("HTTPRequest");
        
        /// THESE THREE LINES:
        
        //httpRequest.Request("https://www.reddit.com/r/worldnews/"); // HTTPRequest
        this.DoHTTPClient().ContinueWith(t => { }); // HTTPClient
        //this.DoDotNetHttpClient().ContinueWith(t => { }); // HttpClient (dotnet)
    }
    
    private async Task DoDotNetHttpClient()
    {
        try
        {
            HttpClient http = new HttpClient();
            var response = http.GetAsync("https://www.reddit.com/r/worldnews/").Result;
            if (!response.IsSuccessStatusCode)
            {
                this.label.Text = response.StatusCode.ToString();
                return;
            }
            HttpContent content = response.Content;
            this.label.Text = content.ReadAsStringAsync().Result;
        }
        catch (Exception e)
        {
            GD.Print(e);
            this.label.Text = e.ToString();
            return;
        }
    }

    public async Task DoHTTPClient()
    {
        Error err;
        HTTPClient http = new HTTPClient(); // Create the client.

        err = http.ConnectToHost("https://www.reddit.com", 80); // Connect to host/port.
        Debug.Assert(err == Error.Ok); // Make sure the connection is OK.

        // Wait until resolved and connected.
        while (http.GetStatus() == HTTPClient.Status.Connecting || http.GetStatus() == HTTPClient.Status.Resolving)
        {
            http.Poll();
            GD.Print("Connecting..." + http.GetStatus());
            OS.DelayMsec(500);
        }

        Debug.Assert(http.GetStatus() ==
                     HTTPClient.Status.Connected); // Check if the connection was made successfully.

        // Some headers.
        string[] headers = { "User-Agent: Test/1.0 (Godot)", "Accept: */*" };

        err = http.Request(HTTPClient.Method.Get, "/r/worldnews/", headers); // Request a page from the site.
        Debug.Assert(err == Error.Ok); // Make sure all is OK.

        // Keep polling for as long as the request is being processed.
        while (http.GetStatus() == HTTPClient.Status.Requesting)
        {
            http.Poll();
            GD.Print("Requesting...");
            if (OS.HasFeature("web"))
            {
                // Synchronous HTTP requests are not supported on the web,
                // so wait for the next main loop iteration.
                await ToSignal(Engine.GetMainLoop(), "idle_frame");
            }
            else
            {
                OS.DelayMsec(500);
            }
        }

        Debug.Assert(http.GetStatus() == HTTPClient.Status.Body ||
                     http.GetStatus() == HTTPClient.Status.Connected); // Make sure the request finished well.

        GD.Print("Response? ", http.HasResponse()); // The site might not have a response.

        // If there is a response...
        if (http.HasResponse())
        {
            headers = http.GetResponseHeaders(); // Get response headers.
            GD.Print("Code: ", http.GetResponseCode()); // Show response code.
            GD.Print("Headers:");
            foreach (string header in headers)
            {
                // Show headers.
                GD.Print(header);
            }

            if (http.IsResponseChunked())
            {
                // Does it use chunks?
                GD.Print("Response is Chunked!");
            }
            else
            {
                // Or just Content-Length.
                GD.Print("Response Length: ", http.GetResponseBodyLength());
            }

            // This method works for both anyways.
            List<byte> rb = new List<byte>(); // List that will hold the data.

            // While there is data left to be read...
            while (http.GetStatus() == HTTPClient.Status.Body)
            {
                http.Poll();
                byte[] chunk = http.ReadResponseBodyChunk(); // Read a chunk.
                if (chunk.Length == 0)
                {
                    // If nothing was read, wait for the buffer to fill.
                    OS.DelayMsec(500);
                }
                else
                {
                    // Append the chunk to the read buffer.
                    rb.AddRange(chunk);
                }
            }

            // Done!
            GD.Print("Bytes Downloaded: ", rb.Count);
            string text = Encoding.ASCII.GetString(rb.ToArray());
            GD.Print(text);
            this.label.Text = "Result:\n" + text;
        }
    }

    public void OnRequestCompleted(int result, int response_code, string[] headers, byte[] body)
    {
        string json = null;
        try
        {
            json = Encoding.UTF8.GetString(body);
        }
        catch (Exception e)
        {
            GD.Print(e);
            label.Text = e.ToString();
            return;
        }

        GD.Print(json);
        if (json != null)
        {
            label.Text = json + " " + response_code.ToString();
        }
    }
}