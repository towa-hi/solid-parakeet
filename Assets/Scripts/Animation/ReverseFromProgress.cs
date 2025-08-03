using UnityEngine;

public class ReverseFromProgress : StateMachineBehaviour
{
    static readonly int SelectProgress = Animator.StringToHash("SelectProgress");
    static readonly int DeselectTransition = Animator.StringToHash("DeselectTransition");
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        float progress = animator.GetFloat(SelectProgress);
        animator.Play(DeselectTransition, 0, progress);
    }
}
