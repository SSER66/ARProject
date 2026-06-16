using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class MarkerTracker : MonoBehaviour
{
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private GameObject characterPrefab;

    private void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    private void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // 新出现的 marker
        foreach (ARTrackedImage addedImage in eventArgs.added)
        {
            // 实例化人物模型，作为 marker 的子物体，自动获得正确位姿
            Instantiate(characterPrefab, addedImage.transform);
        }

        // marker 位置更新（留空，因为物体作为子物体会自动跟随）
        
        // 消失的 marker
        foreach (ARTrackedImage removedImage in eventArgs.removed)
        {
            // 销毁 marker 下的所有子物体（就是我们的人物模型）
            foreach (Transform child in removedImage.transform)
            {
                Destroy(child.gameObject);
            }
        }
    }
}