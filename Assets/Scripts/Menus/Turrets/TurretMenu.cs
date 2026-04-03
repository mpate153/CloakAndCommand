using UnityEngine;

public class TurretMenu : MonoBehaviour
{
    bool isDown = false;
    RectTransform rect;

    private void Start()
    {
        rect = GetComponent<RectTransform>();
    }

    public void ToggleMenu()
    {
        if (isDown)
        {
            MoveUp();
        }
        else
        {
            MoveDown();
        }
    }

    public void ToggleUp()
    {
        if (isDown)
        {
            MoveUp();
        }
    }

    void MoveDown()
    {
        rect.anchoredPosition -= new Vector2(0, rect.rect.height);
        isDown = true;
    }

    void MoveUp()
    {
        rect.anchoredPosition += new Vector2(0, rect.rect.height);
        isDown = false;
    }
}
