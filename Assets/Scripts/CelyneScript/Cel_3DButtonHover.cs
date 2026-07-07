using UnityEngine;
using UnityEngine.EventSystems;

public class Cel_3DButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("3D Models to Color")]
    [Tooltip("The mesh renderers of the 3D text/models inside the button.")]
    public MeshRenderer[] targetRenderers;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color hoverColor = Color.gray;
    public Color pressedColor = new Color(0.3f, 0.3f, 0.3f);

    private void Start()
    {
        // If not assigned manually, try to find all MeshRenderers in children (the 3D text and models)
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<MeshRenderer>();
        }
        
        SetColor(normalColor);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetColor(hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetColor(normalColor);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetColor(pressedColor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SetColor(hoverColor); // Stay hovered after releasing if mouse is still over
    }

    private void SetColor(Color color)
    {
        foreach (var renderer in targetRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = color;
            }
        }
    }
}
