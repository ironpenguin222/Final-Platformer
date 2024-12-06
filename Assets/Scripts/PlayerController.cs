using Unity.VisualScripting.Antlr3.Runtime.Collections;
using UnityEngine;
using System.Collections;

public enum PlayerDirection
{
    left, right
}

public enum PlayerState
{
    idle, walking, jumping, dead
}

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Rigidbody2D body;
    private PlayerDirection currentDirection = PlayerDirection.right;
    public PlayerState currentState = PlayerState.idle;
    public PlayerState previousState = PlayerState.idle;

    [Header("Horizontal")]
    public float maxSpeed = 5f;
    public float accelerationTime = 0.25f;
    public float decelerationTime = 0.15f;
    public float dashes = 1;
    public float dashDist = 5;
    private bool isDashing = false;
    private float dashTimer = 0f;
    private const float dashDuration = 0.2f;

    [Header("Vertical")]
    public float apexHeight = 3f;
    public float apexTime = 0.5f;

    [Header("Ground Checking")]
    public float groundCheckOffset = 0.5f;
    public Vector2 groundCheckSize = new(0.4f, 0.1f);
    public LayerMask groundCheckMask;

    private float accelerationRate;
    private float decelerationRate;

    private float gravity;
    private float initialJumpSpeed;

    private bool isGrounded = false;
    public bool isDead = false;

    private Vector3 velocity;

    public void Start()
    {
        body.gravityScale = 0;

        accelerationRate = maxSpeed / accelerationTime;
        decelerationRate = maxSpeed / decelerationTime;

        gravity = -2 * apexHeight / (apexTime * apexTime);
        initialJumpSpeed = 2 * apexHeight / apexTime;
    }

    public void Update()
    {
        previousState = currentState;

        CheckForGround();

        Vector3 playerInput = new Vector2();
        playerInput.x = Input.GetAxisRaw("Horizontal");

        if (isDead)
        {
            currentState = PlayerState.dead;
        }

        switch(currentState)
        {
            case PlayerState.dead:
                // do nothing - we ded.
                break;
            case PlayerState.idle:
                if (!isGrounded) currentState = PlayerState.jumping;
                else if (velocity.x != 0) currentState = PlayerState.walking;
                break;
            case PlayerState.walking:
                if (!isGrounded) currentState = PlayerState.jumping;
                else if (velocity.x == 0) currentState = PlayerState.idle;
                break;
            case PlayerState.jumping:
                if (isGrounded)
                {
                    if (velocity.x != 0) currentState = PlayerState.walking;
                    else currentState = PlayerState.idle;
                }
                break;
        }

        MovementUpdate(playerInput);
        JumpUpdate();

        if (!isGrounded)
            velocity.y += gravity * Time.deltaTime;
        else
            velocity.y = 0;
        if (isGrounded)
            dashes = 1;

        body.velocity = velocity;
    }



    private void MovementUpdate(Vector3 playerInput)
    {
        if (playerInput.x < 0)
            currentDirection = PlayerDirection.left;
        else if (playerInput.x > 0)
            currentDirection = PlayerDirection.right;

        if (playerInput.x != 0 && !isDashing)
        {
            velocity.x += accelerationRate * playerInput.x * Time.deltaTime;
            velocity.x = Mathf.Clamp(velocity.x, -maxSpeed, maxSpeed);
        }
        else
        {
            if (velocity.x > 0)
            {
                velocity.x -= decelerationRate * Time.deltaTime;
                velocity.x = Mathf.Max(velocity.x, 0);
            }
            else if (velocity.x < 0)
            {
                velocity.x += decelerationRate * Time.deltaTime;
                velocity.x = Mathf.Min(velocity.x, 0);
            }
        }
        if (Input.GetKeyDown(KeyCode.Q) && dashes > 0 && !isDashing)
        {
            isDashing = true;
            dashTimer = dashDuration;
            dashes -= 1;

            Vector3 dashDirection = new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (dashDirection.x == 0 && dashDirection.y == 0)
            {
                if (currentDirection == PlayerDirection.right)
                {
                    dashDirection = Vector3.right;
                }
                else if (currentDirection == PlayerDirection.left)
                {
                    dashDirection = Vector3.left;
                }
            }
            else
            {
                float magnitude = Mathf.Sqrt(dashDirection.x * dashDirection.x + dashDirection.y * dashDirection.y);
                dashDirection.x /= magnitude;
                dashDirection.y /= magnitude;
            }

            velocity = dashDirection * (dashDist / dashDuration);
        
    }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
            }
        }
    }

    private void JumpUpdate()
    {
        if (isGrounded && Input.GetButton("Jump"))
        {
            velocity.y = initialJumpSpeed;
            isGrounded = false;
        }
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("powerup"))
        {
            dashes = 1;
            collision.gameObject.SetActive(false);
            StartCoroutine(RespawnPowerup(collision.gameObject, 3f));
        }
    }

    private IEnumerator RespawnPowerup(GameObject powerup, float delay)
    {
        yield return new WaitForSeconds(delay);
        powerup.SetActive(true); 
    }

    

    private void CheckForGround()
    {
        isGrounded = Physics2D.OverlapBox(
            transform.position + Vector3.down * groundCheckOffset,
            groundCheckSize,
            0,
            groundCheckMask);
    }

    public void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position + Vector3.down * groundCheckOffset, groundCheckSize);
    }

    public bool IsWalking()
    {
        return velocity.x != 0;
    }
    public bool IsGrounded()
    {
        return isGrounded;
    }

    public PlayerDirection GetFacingDirection()
    {
        return currentDirection;
    }
}
