using UnityEngine;

public class PlayerVisuals : MonoBehaviour
{
    public Animator animator;
    public SpriteRenderer bodyRenderer;
    public PlayerController playerController;

    private readonly int idleHash = Animator.StringToHash("Idle");
    private readonly int walkingHash = Animator.StringToHash("Walking");
    private readonly int jumpingHash = Animator.StringToHash("Jumping");
    private readonly int deadHash = Animator.StringToHash("Dead");

    public void Update()
    {
        UpdateVisuals();

        switch (playerController.GetFacingDirection())
        {
            case PlayerDirection.left:
                bodyRenderer.flipX = true;
                break;
            case PlayerDirection.right:
                bodyRenderer.flipX = false;
                break;
        }
    }

    private void UpdateVisuals()
    {
        if (playerController.previousState != playerController.currentState)
        {
            switch (playerController.currentState)
            {
                case PlayerState.idle:
                    animator.CrossFade(idleHash, 0);
                    break;
                case PlayerState.walking:
                    animator.CrossFade(walkingHash, 0);
                    break;
                case PlayerState.jumping:
                    animator.CrossFade(jumpingHash, 0);
                    break;
                case PlayerState.dead:
                    animator.CrossFade(deadHash, 0);
                    break;
            }
        }
    }
}
