using OpenAI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

namespace Samples.Whisper
{
    public class Whisper : MonoBehaviour
    {
        [SerializeField] private Button recordButton;
        [SerializeField] private Image progressBar;
        [SerializeField] private Text message;
        [SerializeField] private Dropdown dropdown;

        private readonly string fileName = "output.wav";
        private readonly int duration = 5;
        private readonly string apiKeyUrl = "https://drive.google.com/uc?export=download&id=1qz_udb7R2-3KuMr5NqloUamCDNI1sFHw";

        private AudioClip clip;
        private bool isRecording;
        private float time;
        private OpenAIApi openai;

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            dropdown.options.Add(new Dropdown.OptionData("Microphone not supported on WebGL"));
#else
            foreach (var device in Microphone.devices)
            {
                dropdown.options.Add(new Dropdown.OptionData(device));
            }
            recordButton.onClick.AddListener(StartRecording);
            dropdown.onValueChanged.AddListener(ChangeMicrophone);

            var index = PlayerPrefs.GetInt("user-mic-device-index");
            dropdown.SetValueWithoutNotify(index);

            // Fetch API Key from Google Drive
            StartCoroutine(GetApiKey());
#endif
        }

        private void ChangeMicrophone(int index)
        {
            PlayerPrefs.SetInt("user-mic-device-index", index);
        }

        private void StartRecording()
        {
            isRecording = true;
            recordButton.enabled = false;

            var index = PlayerPrefs.GetInt("user-mic-device-index");

#if !UNITY_WEBGL
            clip = Microphone.Start(dropdown.options[index].text, false, duration, 44100);
#endif
        }

        private void EndRecording()
        {
            message.text = "Transcripting...";

#if !UNITY_WEBGL
            Microphone.End(null);
#endif

            byte[] data = SaveWav.Save(fileName, clip);

            var req = new CreateAudioTranscriptionsRequest
            {
                FileData = new FileData() { Data = data, Name = "audio.wav" },
                Model = "whisper-1",
                Language = "en"
            };

            StartCoroutine(ProcessTranscription(req));
        }

        private void Update()
        {
            if (isRecording)
            {
                time += Time.deltaTime;
                progressBar.fillAmount = time / duration;

                if (time >= duration)
                {
                    time = 0;
                    isRecording = false;
                    EndRecording();
                }
            }
        }

        private IEnumerator GetApiKey()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(apiKeyUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonData = request.downloadHandler.text;
                    ApiKeyData apiKeyData = JsonUtility.FromJson<ApiKeyData>(jsonData);
                    openai = new OpenAIApi(apiKeyData.api_key);
                }
                else
                {
                    Debug.LogError("Failed to fetch API key: " + request.error);
                }
            }
        }

        private IEnumerator ProcessTranscription(CreateAudioTranscriptionsRequest req)
        {
            if (openai == null)
            {
                Debug.LogError("OpenAI API is not initialized. API key might be missing.");
                yield break;
            }

            var task = openai.CreateAudioTranscription(req);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.Exception != null)
            {
                Debug.LogError("Error processing transcription: " + task.Exception.Message);
                message.text = "Error processing transcription.";
            }
            else
            {
                progressBar.fillAmount = 0;
                message.text = task.Result.Text;
                recordButton.enabled = true;
            }
        }

        [System.Serializable]
        private class ApiKeyData
        {
            public string api_key;
            public string organization;
        }
    }
}


