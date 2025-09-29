using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GeminiOCRHandler : MonoBehaviour
{
    [SerializeField] private string apiKey = "---------------------";

    public IEnumerator SendToGeminiOCR(byte[] imageBytes, System.Action<string> onResult)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

        string base64Image = System.Convert.ToBase64String(imageBytes);

        // JSON payload (similar to Python)
        string jsonPayload = @"
        {
            ""contents"": [
                {
                    ""parts"": [
                        { ""text"": ""Extract text from this image. Return only the text you see."" },
                        {
                            ""inline_data"": {
                                ""mime_type"": ""image/png"",
                                ""data"": """ + base64Image + @"""
                            }
                        }
                    ]
                }
            ]
        }";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Gemini OCR Response: " + request.downloadHandler.text);

                try
                {
                    // Parse JSON → just grab the text response
                    var result = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                    if (result != null && result.candidates.Length > 0)
                    {
                        onResult(result.candidates[0].content.parts[0].text);
                    }
                    else
                    {
                        onResult("No OCR text found.");
                    }
                }
                catch
                {
                    onResult("Error parsing Gemini response.");
                }
            }
            else
            {
                Debug.LogError("Gemini OCR network error: " + request.error);
                onResult(" API Error: " + request.error);
            }
        }
    }
}

[System.Serializable]
public class GeminiResponse
{
    public Candidate[] candidates;
}

[System.Serializable]
public class Candidate
{
    public Content content;
}

[System.Serializable]
public class Content
{
    public Part[] parts;
}

[System.Serializable]
public class Part
{
    public string text;
}
