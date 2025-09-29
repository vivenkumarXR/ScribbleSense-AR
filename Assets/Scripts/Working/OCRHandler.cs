using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

public class OCRHandler : MonoBehaviour
{
    public string apiKey = "YOUR_OCR_SPACE_API_KEY";

    public IEnumerator SendToOCR(Texture2D image, Action<string> callback)
    {
        byte[] bytes = image.EncodeToPNG();
        // Save locally for debugging
        string path = Application.persistentDataPath + "/ocr_debug.png";
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log("-----------------------------------------Saved OCR image at: " + path);
        string base64Image = Convert.ToBase64String(bytes);

        WWWForm form = new WWWForm();
        form.AddField("apikey", apiKey);
        form.AddField("language", "eng");
        form.AddField("isOverlayRequired", "false");
        form.AddField("base64Image", "data:image/png;base64," + base64Image);

        using (UnityWebRequest www = UnityWebRequest.Post("https://api.ocr.space/parse/image", form))
        {
            //yield return www.SendWebRequest();

            //if (www.result == UnityWebRequest.Result.Success)
            //{
            //    Debug.Log("------------------------------------------OCR Response: " + www.downloadHandler.text);
            //    callback(www.downloadHandler.text);
            //}
            //else
            //{
            //    Debug.LogError("----------------------------------------OCR Error: " + www.error);
            //    callback("Error");
            //}

            www.timeout = 20; // 20s max
            yield return www.SendWebRequest();

            // First: Check Unity-level errors
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("----------- UnityWebRequest Error: " + www.error);
                Debug.LogError("--------------   (Check Internet connection / AndroidManifest INTERNET permission)");
                callback("Error");
                yield break;
            }

            string json = www.downloadHandler.text;
            Debug.Log(" --------Raw OCR Response: " + json);

            // Second: Check for API-level errors
            if (json.Contains("\"IsErroredOnProcessing\":true") || json.Contains("ErrorMessage"))
            {
                Debug.LogError("-------- OCR API reported error. Possible reasons:");
                Debug.LogError("--------   - Invalid API Key (using 'helloworld' hits strict limits)");
                Debug.LogError("--------   - Daily/monthly quota exceeded");
                Debug.LogError("---------   - File too large (try resizing before sending)");
                Debug.LogError("----------   - Unsupported format");
                callback("Error");
                yield break;
            }

            // If no obvious errors, pass result back
            callback(json);
        }
    }
}
