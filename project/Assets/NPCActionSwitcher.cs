using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class NPCActionSwitcher : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;

    [Header("可以切换的动作名称，必须和 Animator 里的状态名完全一致")]
    public string[] actionStates = new string[]
    {
        "idle1",
        "idle2",
        "listen",
        "talk1",
        "talk2"
    };

    [Header("动作切换间隔")]
    public float minSwitchTime = 4f;
    public float maxSwitchTime = 8f;

    [Header("动作过渡时间")]
    public float fadeTime = 0.25f;

    [Header("是否避免连续播放同一个动作")]
    public bool avoidSameAction = true;

    [Header("是否关闭 Root Motion，防止人物自己走动")]
    public bool disableRootMotion = true;

    private int lastActionIndex = -1;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator != null && disableRootMotion)
        {
            animator.applyRootMotion = false;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(SwitchActionLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator SwitchActionLoop()
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 2f));

        PlayRandomAction();

        while (true)
        {
            float waitTime = Random.Range(minSwitchTime, maxSwitchTime);
            yield return new WaitForSeconds(waitTime);

            PlayRandomAction();
        }
    }

    private void PlayRandomAction()
    {
        if (animator == null)
        {
            Debug.LogWarning(gameObject.name + " 没有 Animator，无法切换动作。");
            return;
        }

        if (actionStates == null || actionStates.Length == 0)
        {
            Debug.LogWarning(gameObject.name + " 没有设置动作列表。");
            return;
        }

        int index = Random.Range(0, actionStates.Length);

        if (avoidSameAction && actionStates.Length > 1)
        {
            int safeCount = 0;

            while (index == lastActionIndex && safeCount < 10)
            {
                index = Random.Range(0, actionStates.Length);
                safeCount++;
            }
        }

        string stateName = actionStates[index];

        int stateHash = Animator.StringToHash(stateName);

        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning(gameObject.name + " 的 Animator 中找不到动作状态：" + stateName);
            return;
        }

        animator.CrossFade(stateName, fadeTime, 0);

        lastActionIndex = index;
    }
}

