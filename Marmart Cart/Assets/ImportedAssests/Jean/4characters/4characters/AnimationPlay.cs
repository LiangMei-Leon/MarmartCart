using UnityEngine;

public class PlayAnimation : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        // 获取模型上的 Animator 组件
        animator = GetComponent<Animator>();

        // 确保 Animator 存在并触发播放动画
        if (animator != null)
        {
            animator.Play("Take 001"); // 替换为你的动画状态的名字
        }
    }
}
