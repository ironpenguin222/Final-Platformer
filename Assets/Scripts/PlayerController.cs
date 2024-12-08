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
        //Starting calculations

        body.gravityScale = 0;

        accelerationRate = maxSpeed / accelerationTime;
        decelerationRate = maxSpeed / decelerationTime;

        gravity = -2 * apexHeight / (apexTime * apexTime);
        initialJumpSpeed = 2 * apexHeight / apexTime;
    }

    public void Update()
    {
        // Sets state to current state

        previousState = currentState;

        // Checks if player is touching anything

        CheckForGround();
        CheckForWall();

        // Gets player input

        Vector3 playerInput = new Vector2();
        playerInput.x = Input.GetAxisRaw("Horizontal");

        // Checks if player is dead

        if (isDead)
        {
            currentState = PlayerState.dead;
        }

        // Switches player state based on behavior

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

        // Checks to see if conditions are right for wallsliding, and allowing for walljump if correct

        if (isTouchingWall && !isGrounded && velocity.y < 0)
        {
            velocity.y = Mathf.Max(velocity.y, wallSlideSpeed);
        }
        if (Input.GetButtonDown("Jump") && isTouchingWall && !isGrounded)
        {
            WallJump();
        }

        // Player movement and jumping

        MovementUpdate(playerInput);
        JumpUpdate();

        // Applies gravity if player is not on the ground

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

        // Allows for dashing if player lands on ground

        if (isGrounded)
            dashes = 1;

        body.velocity = velocity;

        // Grappling states, E to shoot out the grapple hook, and it gets released upon lifting the key

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryGrapple();
        }

        if (Input.GetKeyUp(KeyCode.E))
        {
            ReleaseGrapple();
        }

        // Player is able to swing/see the rope while grappling

        if (isGrappling)
        {
            Swinging();
            SeeRope();
        }

        // Player can reel in or out from the grapple hook

        if (Input.GetKey(KeyCode.W) && !isGrounded)
        {
            ReelIn();
        }
        if (Input.GetKey(KeyCode.S))
        {
            ReelOut();
        }
    }

    // Checks the direction of the wall jumping off of and applies the velocity to the player pushing them off the wall

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

    // Checks for wall using a raycast based on the player location, direction, how far it should check, and what it should check for (reusing some values) to figure out if playuer is touching the wall.

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
        //Fires raycast from player in direction of mouse

        RaycastHit2D hit = Physics2D.Raycast(transform.position, GetMouseDirection(), grappleRange, grappleLayer);

        // Makes sure it hit something, and then enables the distance joint from the player to the point that was hit by the grapple, and then sets the state 

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
        //Makes sure that player isn't too close to the grapple point to reel in, and pushes the player towards the grapple point

        if (dj.distance > 1f)
        {
            dj.distance -= 4 * Time.deltaTime;

            Vector2 directionToGrapple = (grapplePoint - (Vector2)transform.position).normalized;
            body.AddForce(directionToGrapple * 4, ForceMode2D.Force);
        }
    }

    private void ReelOut()
    {
        //Makes sure that player isn't too far from the grapple point in accordance to the range to reel out, and pushes the player away from the grapple point

        if (dj.distance < grappleRange)
        {
            dj.distance += 4 * Time.deltaTime;

            Vector2 directionAwayFromGrapple = ((Vector2)transform.position - grapplePoint).normalized;
            body.AddForce(directionAwayFromGrapple * 4, ForceMode2D.Force);
        }
    }

    private void Swinging()
    {
        // Applies force to the player in the direction of key pressed

        if (Input.GetKey(KeyCode.A))
        {
            body.AddForce(Vector2.left * swingForce * 10, ForceMode2D.Force);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            body.AddForce(Vector2.right * swingForce * 10, ForceMode2D.Force);
        }
    }

    // Disables grapple upon release

    private void ReleaseGrapple()
    {
        dj.enabled = false;
        isGrappling = false;
        lr.enabled = false;
    }

    // Line renderer shows the player rope for visual clarity by drawing a line between the player position and the grapple point

    private void SeeRope()
    {
        lr.SetPosition(0, transform.position);
        lr.SetPosition(1, grapplePoint);
    }

    // Gets the direction of the mouse through ScreenToWorldPoint and returns the value in a useable way

    Vector2 GetMouseDirection()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return (mousePosition - transform.position).normalized;
    }



    private void MovementUpdate(Vector3 playerInput)
    {
        // Takes player input for direction

        if (playerInput.x < 0)
            currentDirection = PlayerDirection.left;
        else if (playerInput.x > 0)
            currentDirection = PlayerDirection.right;

        // Moves the player if the input is greater than 0, and applies acceleration and deceleration according to velocity values

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

        // Dashing logic, makes player dash based on timer, getting the player's direction, then setting the magnitude and velocity

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

        // Dash timer for the player dashing

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
            }
        }
    }

    // Jumping for tha player

    private void JumpUpdate()
    {
        if (isGrounded && Input.GetButton("Jump"))
        {
            velocity.y = initialJumpSpeed;
            isGrounded = false;
        }
    }

    // Checks if player touches powerup that restores dashes and applies logic

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("powerup"))
        {
            dashes = 1;
            collision.gameObject.SetActive(false);
            StartCoroutine(RespawnPowerup(collision.gameObject, 3f));
        }
    }

    // Coroutine to check for when the powerup should respawn

    private IEnumerator RespawnPowerup(GameObject powerup, float delay)
    {
        yield return new WaitForSeconds(delay);
        powerup.SetActive(true);
    }

    // Ground checking logic, that makes sure when player is on ground they are given the state of grounded

    private void CheckForGround()
    {
        isGrounded = Physics2D.OverlapBox(
            transform.position + Vector3.down * groundCheckOffset,
            groundCheckSize,
            0,
            groundCheckMask);

    }

    // Visualizes the player's grounded hitbox

    public void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position + Vector3.down * groundCheckOffset, groundCheckSize);
    }

    // IsWalking Bool based on if player is moving

    public bool IsWalking()
    {
        return velocity.x != 0;
    }

    // IsGrounded check

    public bool IsGrounded()
    {
        return isGrounded;
    }

    // Player direction

    public PlayerDirection GetFacingDirection()
    {
        return currentDirection;
    }
}

