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
    public float maxFallSpeed = -10f;

    [Header("Wall Jump")]
    public float wallJumpForce = 10f;
    public float wallSlideSpeed = -2f;
    public Vector2 wallJumpDirection = new Vector2(1, 1).normalized;
    private bool isTouchingWall = false;

    [Header("Ground Checking")]
    public float groundCheckOffset = 0.5f;
    public Vector2 groundCheckSize = new(0.4f, 0.1f);
    public LayerMask groundCheckMask;

    [Header("Grapple Settings")]
    public LayerMask grappleLayer;
    public float grappleRange = 15f;
    public float swingForce = 10f;
    private Vector2 grapplePoint;
    private bool isGrappling = false;

    private float accelerationRate;
    private float decelerationRate;
    private float gravity;
    private float initialJumpSpeed;

    private bool isGrounded = false;
    public bool isDead = false;
    public LineRenderer lr;
    public DistanceJoint2D dj;
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
        CheckForWall();

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

        if (isTouchingWall && !isGrounded && velocity.y < 0)
        {
            velocity.y = Mathf.Max(velocity.y, wallSlideSpeed);
        }
        if (Input.GetButtonDown("Jump") && isTouchingWall && !isGrounded)
        {
            WallJump();
        }

        MovementUpdate(playerInput);
        JumpUpdate();

        if (!isGrounded)
        {
            velocity.y += gravity * Time.deltaTime;

            if (velocity.y < maxFallSpeed)
            {
                velocity.y = maxFallSpeed;
            }
        }
        else
        {
            velocity.y = 0;
        }

        if (isGrounded)
            dashes = 1;

        body.velocity = velocity;

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryGrapple();
        }

        if (Input.GetKeyUp(KeyCode.E))
        {
            ReleaseGrapple();
        }

        if (isGrappling)
        {
            Swinging();
            SeeRope();
        }
        if (Input.GetKey(KeyCode.W) && !isGrounded)
        {
            ReelIn();
        }
        if (Input.GetKey(KeyCode.S))
        {
            ReelOut();
        }
    }

    private void WallJump()
    {
        int wallDirection;
        if (currentDirection == PlayerDirection.right)
        {
            wallDirection = -1;
        }
        else
        {
            wallDirection = 1;
        }

        velocity.x = wallJumpDirection.x * wallJumpForce * wallDirection;
        velocity.y = wallJumpDirection.y * wallJumpForce;
        isTouchingWall = false;
    }

    private void CheckForWall()
    {
        Vector2 wallCheckDirection;

        if (currentDirection == PlayerDirection.right)
        {
            wallCheckDirection = Vector2.right;
        }
        else
        {
            wallCheckDirection = Vector2.left;
        }
        isTouchingWall = Physics2D.Raycast(transform.position, wallCheckDirection, groundCheckOffset, groundCheckMask);
    }

    private void TryGrapple()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, GetMouseDirection(), grappleRange, grappleLayer);

        if (hit.collider != null)
        {
            grapplePoint = hit.point;

            dj.enabled = true;
            dj.connectedAnchor = grapplePoint;
            dj.distance = Vector2.Distance(transform.position, grapplePoint);
            isGrappling = true;
            lr.enabled = true;
        }
    }

    private void ReelIn()
    {
        if (dj.distance > 1f)
        {
            dj.distance -= 4 * Time.deltaTime;

            Vector2 directionToGrapple = (grapplePoint - (Vector2)transform.position).normalized;
            body.AddForce(directionToGrapple * 4, ForceMode2D.Force);
        }
    }

    private void ReelOut()
    {
        if (dj.distance < grappleRange)
        {
            dj.distance += 4 * Time.deltaTime;

            Vector2 directionAwayFromGrapple = ((Vector2)transform.position - grapplePoint).normalized;
            body.AddForce(directionAwayFromGrapple * 4, ForceMode2D.Force);
        }
    }

    private void Swinging()
    {
        if (Input.GetKey(KeyCode.A))
        {
            body.AddForce(Vector2.left * swingForce * 10, ForceMode2D.Force);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            body.AddForce(Vector2.right * swingForce * 10, ForceMode2D.Force);
        }
    }

    private void ReleaseGrapple()
    {
        dj.enabled = false;
        isGrappling = false;
        lr.enabled = false;
    }


    private void SeeRope()
    {
        lr.SetPosition(0, transform.position);
        lr.SetPosition(1, grapplePoint);
    }

    Vector2 GetMouseDirection()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return (mousePosition - transform.position).normalized;
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

