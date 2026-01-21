using UnityEngine;

public class PlayerControls : MonoBehaviour
{
    [Header("Input Names")]
    public string horizontalAxis = "Horizontal";
    public string jumpButton = "Jump";
    public string fire1Button = "Fire1";
    public string fire2Button = "Fire2";
    public string fire3Button = "Fire3";
    public string rollButton = "Fire2";
    public string interactButton = "Interact";

    [Header("Current Input State")]

    public float horizontalInput;
    public bool jumpPressed;
    public bool fire1Pressed;
    public bool fire2Pressed;
    public bool fire3Pressed;
    public bool rollPressed;
    public bool interactPressed;

    void Update()
    {
        // Read all inputs once per frame
        horizontalInput = Input.GetAxisRaw(horizontalAxis);
        jumpPressed = Input.GetButtonDown(jumpButton);
        fire1Pressed = Input.GetButtonDown(fire1Button);
        fire2Pressed = Input.GetButtonDown(fire2Button);
        fire3Pressed = Input.GetButtonDown(fire3Button);
        rollPressed = Input.GetButtonDown(rollButton);
        interactPressed = Input.GetButtonDown(interactButton);
    }
}