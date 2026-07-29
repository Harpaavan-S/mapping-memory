using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Button button;
    private Text buttonText;
    private Color originalButtonColor;
    private Color originalTextColor;
    
    void Start()
    {
        button = GetComponent<Button>();
        buttonText = GetComponentInChildren<Text>();
        
        if (button != null) originalButtonColor = button.image.color;
        if (buttonText != null) originalTextColor = buttonText.color;
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && buttonText != null)
        {
            // Change button to white, text to button's original color
            button.image.color = Color.white;
            buttonText.color = originalButtonColor;
        }
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        if (button != null && buttonText != null)
        {
            // Revert colors
            button.image.color = originalButtonColor;
            buttonText.color = originalTextColor;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
            transform.localScale = Vector3.one * 1.1f;  // Slight hover scale
    }

    public void OnPointerExit(PointerEventData eventData)
    {
            transform.localScale = Vector3.one;
    }
}

    