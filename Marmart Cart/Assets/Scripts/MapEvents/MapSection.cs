using UnityEngine;
using TMPro;

public enum SectionId { Red, Green, Blue, Purple }

public class MapSection : MonoBehaviour
{
    public SectionId sectionId;

    [Header("Label UI")]
    [SerializeField] private TextMeshPro labelTMP;

    [Header("Ring Mesh")]
    [SerializeField] private Renderer ringRenderer;
    [Tooltip("Shader color property used for the ring tint (URP Lit = _BaseColor, Standard = _Color).")]
    [SerializeField] private string ringColorProperty = "_BaseColor";

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color activeColor = new Color(1f, 0.8f, 0.2f);

    [Header("Event Generator for this section")]
    public EventItemGenerator eventGenerator;

    public void SetNormal()
    {
        ApplyColor(normalColor);
    }

    public void SetWarning()
    {
        ApplyColor(warningColor);
    }

    public void SetActive()
    {
        ApplyColor(activeColor);
    }

    // ----------------- INTERNAL -----------------

    private void ApplyColor(Color c)
    {
        // Text
        if (labelTMP != null)
            labelTMP.color = c;

        // Ring
        if (ringRenderer != null)
        {
            // Use material (instance), not sharedMaterial
            Material mat = ringRenderer.material;

            if (!string.IsNullOrEmpty(ringColorProperty) && mat.HasProperty(ringColorProperty))
            {
                mat.SetColor(ringColorProperty, c);
            }
            else if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", c);
            }
            else if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", c);
            }
        }
    }
}
