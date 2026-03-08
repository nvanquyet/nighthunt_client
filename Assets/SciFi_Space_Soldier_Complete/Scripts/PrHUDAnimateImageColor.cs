using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PrHUDAnimateImageColor : MonoBehaviour
{

    public float Speed = 1.0f;
    public AnimationCurve colorCurve;
    public Color targetColor = new Color(0, 0, 0, 1);
    /*public bool splitAlpha = false;
    public AnimationCurve alphaCurve;
    public float targetAlpha = 0.0f;*/
    private Color tempColor;
    private Image imageComponent;
    private Color originalColor;
    // Use this for initialization
    void Start()
    {
        imageComponent = GetComponent<Image>();
        originalColor = imageComponent.color;
    }

    // Update is called once per frame
    void Update()
    {
        tempColor = Color.Lerp(originalColor, targetColor, colorCurve.Evaluate(Time.time * Speed));
        /*if (splitAlpha)
            tempColor.a = Mathf.Lerp(originalColor.a, targetAlpha, alphaCurve.Evaluate(Time.time * Speed));*/
        imageComponent.color = tempColor;
    }

}

