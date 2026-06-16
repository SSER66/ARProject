using System.Collections;
using System.Net;
using TMPro;
using UnityEngine;

public class FamilyChatManager : MonoBehaviour
{
    [Header("VR UI")]
    public TMP_Text titleText;
    public TMP_Text roundText;
    public TMP_Text currentSpeakerText;
    public TMP_Text chatHistoryText;
    public TMP_Text emotionScoreText;
    public TMP_Text hintText;
    public TMP_InputField userInputField;

    [Header("AI")]
    public AIRelativeConfig relativeConfig;
    public DeepSeekChatClient chatClient;

    private int round = 0;
    private string currentSpeaker = "系统";
    private string aiSpeakerName = "AI亲戚";
    private bool waitingForResponse = false;
    private bool chatSessionInitialized = false;

    private void Awake()
    {
        // 全局 TLS 1.2 设置（保证所有 HTTPS 请求都用 TLS 1.2）
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        if (chatClient == null)
        {
            chatClient = GetComponent<DeepSeekChatClient>();
        }

        if (relativeConfig != null && !string.IsNullOrWhiteSpace(relativeConfig.relativeName))
        {
            aiSpeakerName = relativeConfig.relativeName;
        }

        EnsureChatClientInitialized();
    }

    public void StartChat()
    {
        round = 1;
        currentSpeaker = "我";
        waitingForResponse = false;

        roundText.text = $"第 {round} 轮";
        currentSpeakerText.text = $"当前发言人：{currentSpeaker}";
        chatHistoryText.text = "系统：家庭群聊开始。\n";
        hintText.text = "提示：可以打字，或按住语音键输入。";

        if (chatClient != null)
        {
            EnsureChatClientInitialized(forceReinitialize: true);
        }
        else
        {
            hintText.text = "提示：未找到 DeepSeekChatClient，当前只显示本地消息。";
        }
    }

    public void NextTurn()
    {
        round++;
        currentSpeaker = "我";

        roundText.text = $"第 {round} 轮";
        currentSpeakerText.text = $"当前发言人：{currentSpeaker}";
        chatHistoryText.text += $"系统：进入第 {round} 轮。\n";
    }

    public void EndChat()
    {
        chatHistoryText.text += "系统：群聊结束。\n";
        hintText.text = "提示：对话已结束。";
        waitingForResponse = false;
    }

    public void SendText()
    {
        if (waitingForResponse)
        {
            hintText.text = "提示：AI 正在回复，请稍候。";
            return;
        }

        string msg = userInputField.text.Trim();
        if (string.IsNullOrEmpty(msg))
            return;

        AppendChatLine("我", msg);
        userInputField.text = string.Empty;

        if (chatClient == null)
        {
            hintText.text = "提示：未配置 AI 客户端，当前只记录本地消息。";
            return;
        }

        EnsureChatClientInitialized();

        StartCoroutine(SendToAI(msg));
    }

    public void ClearInput()
    {
        userInputField.text = "";
    }

    public void SetVoiceText(string text)
    {
        userInputField.text = text;
    }

    public void SetEmotionScore(string scoreText)
    {
        emotionScoreText.text = "情绪评分：" + scoreText;
    }

    private IEnumerator SendToAI(string userText)
    {
        waitingForResponse = true;
        hintText.text = "提示：AI 思考中...";

        // 延迟一帧，确保 DeepSeekChatClient 初始化完成
        yield return null;

        yield return chatClient.SendChatRequest(
            userText,
            assistantReply =>
            {
                AppendChatLine(aiSpeakerName, assistantReply);
                hintText.text = "提示：收到 AI 回复。";
                waitingForResponse = false;
            },
            errorMessage =>
            {
                AppendChatLine("系统", $"AI 请求失败：{errorMessage}");
                hintText.text = "提示：AI 请求失败，请检查 Key 或网络。";
                waitingForResponse = false;
            });
    }

    private void AppendChatLine(string speaker, string text)
    {
        chatHistoryText.text += $"{speaker}：{text}\n";
    }

    private void EnsureChatClientInitialized(bool forceReinitialize = false)
    {
        if (chatClient == null)
        {
            return;
        }

        if (!forceReinitialize && chatSessionInitialized)
        {
            return;
        }

        string systemPrompt = relativeConfig != null ? relativeConfig.GetActiveSystemPrompt() : string.Empty;
        chatClient.Initialize(systemPrompt);
        chatSessionInitialized = true;

        if (relativeConfig != null && !string.IsNullOrWhiteSpace(relativeConfig.relativeName))
        {
            aiSpeakerName = relativeConfig.relativeName;
        }
    }
}
