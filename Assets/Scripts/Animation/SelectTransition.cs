using UnityEngine;

public class SelectTransition : StateMachineBehaviour
{
    static readonly int SelectProgress = Animator.StringToHash("SelectProgress");

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetFloat(SelectProgress, stateInfo.normalizedTime);
    }
}
