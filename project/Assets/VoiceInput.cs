using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Collections;

public class VoiceInputManager : MonoBehaviour
{
    public FamilyChatManager familyChatManager;   // 用于显示识别文字的脚本引用

    private DictationRecognizer dictationRecognizer;
    private string recognizedText = "";            // 累积的识别结果
    private bool isRecording = false;

    void Start()
    {
        // 检查是否有麦克风设备（DictationRecognizer 会自动使用默认麦克风）
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("未检测到麦克风设备，语音输入功能不可用！");
            return;
        }

        // 创建 DictationRecognizer 实例并绑定事件
        dictationRecognizer = new DictationRecognizer();
        dictationRecognizer.DictationResult += OnDictationResult;
        dictationRecognizer.DictationComplete += OnDictationComplete;
        dictationRecognizer.DictationError += OnDictationError;
        dictationRecognizer.DictationHypothesis += OnDictationHypothesis;

        Debug.Log("语音识别初始化完成，设备：" + Microphone.devices[0]);
    }

    // 开始录音（点击按钮时调用）
    public void BeginRecord()
    {
        if (isRecording) return;
        if (dictationRecognizer == null)
        {
            Debug.LogError("DictationRecognizer 未初始化");
            return;
        }

        recognizedText = "";              // 清空之前的识别文本
        dictationRecognizer.Start();      // 开始语音识别（自动启用麦克风）
        isRecording = true;
        Debug.Log("开始录音…");
    }

    // 结束录音（松开按钮或再次点击时调用）
    public void EndRecord()
    {
        if (!isRecording) return;
        if (dictationRecognizer == null) return;

        dictationRecognizer.Stop();       // 停止识别，之后会触发 OnDictationComplete 事件
        isRecording = false;
        Debug.Log("结束录音，等待识别结果…");
    }

    // 识别过程中产生的临时结果（可用于实时显示，但不是最终结果）
    private void OnDictationHypothesis(string hypothesis)
    {
        Debug.Log("识别中（临时）: " + hypothesis);
        // 可选：实时显示假设文字
        // familyChatManager.SetVoiceText(hypothesis);
    }

    // 每条完整短语识别完成时调用（最终结果的一部分）
    private void OnDictationResult(string text, ConfidenceLevel confidence)
    {
        Debug.Log("识别结果片段: " + text + " (置信度: " + confidence + ")");
        // 累积结果（通常最终结果会分段多次触发）
        recognizedText += text + " ";
    }

    // 整个识别过程结束时调用（成功或失败）
    private void OnDictationComplete(DictationCompletionCause cause)
    {
        Debug.Log("识别结束，原因: " + cause);

        if (cause == DictationCompletionCause.Complete)
        {
            // 识别成功，将累积的文本传递给 FamilyChatManager
            if (!string.IsNullOrEmpty(recognizedText))
            {
                familyChatManager.SetVoiceText(recognizedText.Trim());
                Debug.Log("最终识别文字: " + recognizedText);
            }
            else
            {
                Debug.LogWarning("没有识别到任何文字");
                familyChatManager.SetVoiceText("（未识别到语音）");
            }
        }
        else if (cause == DictationCompletionCause.TimeoutExceeded)
        {
            Debug.LogWarning("录音超时未检测到有效语音");
            familyChatManager.SetVoiceText("（语音输入超时）");
        }
        else
        {
            Debug.LogError("识别错误: " + cause);
            familyChatManager.SetVoiceText("（语音识别失败）");
        }

        // 重置状态，允许下一次录音
        isRecording = false;
        recognizedText = "";
    }

    // 发生错误时调用
    private void OnDictationError(string error, int hresult)
    {
        Debug.LogError("语音识别错误: " + error + " HRESULT: " + hresult);
        familyChatManager.SetVoiceText("（语音识别出错）");
        isRecording = false;
    }

    // 销毁时释放资源
    private void OnDestroy()
    {
        if (dictationRecognizer != null)
        {
            dictationRecognizer.DictationResult -= OnDictationResult;
            dictationRecognizer.DictationComplete -= OnDictationComplete;
            dictationRecognizer.DictationError -= OnDictationError;
            dictationRecognizer.DictationHypothesis -= OnDictationHypothesis;
            if (dictationRecognizer.Status == SpeechSystemStatus.Running)
                dictationRecognizer.Stop();
            dictationRecognizer.Dispose();
        }
    }
}