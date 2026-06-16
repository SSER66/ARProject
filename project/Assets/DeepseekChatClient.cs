using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class ChatMessage
{
    public string role;
    [TextArea(2, 6)]
    public string content;

    public ChatMessage(string role, string content)
    {
        this.role = role;
        this.content = content;
    }
}

[Serializable]
public class DeepSeekRequest
{
    public string model;
    public List<ChatMessage> messages;
    public float temperature;
    public float top_p;
    public float frequency_penalty;
    public float presence_penalty;
    public int max_tokens;
}

[Serializable]
public class DeepSeekResponse
{
    public Choice[] choices;
    public string error;
}

[Serializable]
public class Choice
{
    public ResponseMessage message;
}

[Serializable]
public class ResponseMessage
{
    public string role;
    public string content;
}

public class DeepSeekChatClient : MonoBehaviour
{
    [Header("DeepSeek API 配置")]
    [Tooltip("如果需要 Bearer 前缀可以直接填写完整字符串，或者只填 API Key。")]
    public string apiKey = "";
    public string apiUrl = "https://api.deepseek.cn/v1/chat/completions";
    public string model = "deepseek-chat";
    [Tooltip("仅用于开发测试：忽略 SSL 证书验证。生产环境请保持关闭。")]
    public bool ignoreSslErrors = false;

    [Header("回复风格")]
    [Range(0f, 2f)]
    [Tooltip("越高越有变化。亲戚闲聊建议 1.1~1.3。")]
    public float temperature = 1.2f;
    [Range(0f, 1f)]
    public float topP = 0.9f;
    [Range(-2f, 2f)]
    [Tooltip("减少口头禅和固定句式的重复。")]
    public float frequencyPenalty = 0.35f;
    [Range(-2f, 2f)]
    [Tooltip("让对话更愿意自然地引出新话题。")]
    public float presencePenalty = 0.15f;
    [Min(32)]
    [Tooltip("限制回答长度，避免亲戚突然开始写小作文。")]
    public int maxTokens = 160;

    [Header("对话历史(多轮会话) ")]
    public List<ChatMessage> messageHistory = new List<ChatMessage>();

    /// <summary>
    /// 初始化对话，将 system prompt 作为第一条消息存入历史。
    /// </summary>
    public void Initialize(string systemPrompt)
    {
        messageHistory.Clear();
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messageHistory.Add(new ChatMessage("system", systemPrompt));
        }
    }

    /// <summary>
    /// 发送用户消息到 DeepSeek，并回调回答。
    /// </summary>
    public IEnumerator SendChatRequest(string userContent, Action<string> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            onError?.Invoke("请先填写 DeepSeek apiKey。\n可以在 ChatDemoUI 中设置。想要快速测试，可先在 Inspector 中填入 apiKey。 ");
            yield break;
        }

        if (string.IsNullOrEmpty(userContent))
        {
            onError?.Invoke("发送内容不能为空。");
            yield break;
        }

        if (!TryNormalizeApiUrl(apiUrl, out string finalApiUrl, out string apiUrlError))
        {
            onError?.Invoke(apiUrlError);
            yield break;
        }

        messageHistory.Add(new ChatMessage("user", userContent));

        DeepSeekRequest requestData = new DeepSeekRequest
        {
            model = string.IsNullOrEmpty(model) ? "deepseek-chat" : model,
            messages = messageHistory,
            temperature = temperature,
            top_p = topP,
            frequency_penalty = frequencyPenalty,
            presence_penalty = presencePenalty,
            max_tokens = maxTokens
        };

        string jsonBody = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(finalApiUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;
            request.SetRequestHeader("Content-Type", "application/json");

            if (ignoreSslErrors)
            {
                request.certificateHandler = new AcceptAllCertificates();
            }

            string authValue = apiKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? apiKey
                : $"Bearer {apiKey}";
            request.SetRequestHeader("Authorization", authValue);

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool hasNetworkError = request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool hasNetworkError = request.isNetworkError || request.isHttpError;
#endif
            if (hasNetworkError)
            {
                string networkError = request.error ?? "未知网络错误";
                if (IsSslError(networkError))
                {
                    onError?.Invoke(
                        $"SSL error: {networkError}. Check system clock, HTTPS access, and apiUrl uses https. For local testing only, enable ignoreSslErrors in Inspector.");
                    yield break;
                }

                onError?.Invoke($"请求失败：{networkError}");
                yield break;
            }

            string json = request.downloadHandler.text;
            string assistantText = ParseAssistantAnswer(json);

            if (string.IsNullOrEmpty(assistantText))
            {
                onError?.Invoke($"DeepSeek 返回异常，原始响应：{json}");
                yield break;
            }

            messageHistory.Add(new ChatMessage("assistant", assistantText));
            onSuccess?.Invoke(assistantText);

            
        }
    }

    private bool TryNormalizeApiUrl(string rawApiUrl, out string normalizedApiUrl, out string error)
    {
        normalizedApiUrl = null;
        error = null;

        string candidate = rawApiUrl?.Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            error = "apiUrl 为空，请在 Inspector 填写 DeepSeek 接口地址。";
            return false;
        }

        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri))
        {
            error = $"apiUrl 格式不正确：{rawApiUrl}";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = $"apiUrl 必须以 http 或 https 开头：{candidate}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = $"apiUrl 缺少主机名：{candidate}";
            return false;
        }

        normalizedApiUrl = uri.AbsoluteUri;
        return true;
    }

    private bool IsSslError(string error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return false;
        }

        string text = error.ToLowerInvariant();
        return text.Contains("ssl") ||
               text.Contains("tls") ||
               text.Contains("certificate") ||
               text.Contains("handshake");
    }

    private string ParseAssistantAnswer(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            DeepSeekResponse response = JsonUtility.FromJson<DeepSeekResponse>(json);
            if (response != null && response.choices != null && response.choices.Length > 0 && response.choices[0].message != null)
            {
                return response.choices[0].message.content?.Trim();
            }
        }
        catch (Exception)
        {
            // 忽略解析异常，后续尝试原始字符串
        }

        return json.Trim();
    }

    private class AcceptAllCertificates : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }
}
