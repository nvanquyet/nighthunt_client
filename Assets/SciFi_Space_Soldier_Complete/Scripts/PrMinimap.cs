using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PrMinimap : MonoBehaviour
{
    // Start is called before the first frame update
    private Bounds sceneBounds;
    public GameObject minimapBackground;
    private GameObject currentMinimapBackground;
    public RectTransform window;
    [HideInInspector]
    public int playerNumber = 0;
    public RenderTexture[] minimapRenderMaps;
    public GameObject minimapRender;

    
    void Start()
    {
        if (minimapBackground)
        {
            sceneBounds = CalculateSceneBounds();

            // Create Background
            currentMinimapBackground = Instantiate(minimapBackground);

            // Move and scale to fit SCENE. 
            currentMinimapBackground.transform.position = new Vector3(sceneBounds.center.x, -10, sceneBounds.center.z);
            currentMinimapBackground.transform.localScale = sceneBounds.extents / 4;
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ScaleAndMove(Vector2 move, float scale, int playerNmb)
    {
        window.localPosition -= new Vector3(move.x, move.y, 0);

        window.transform.localScale = Vector3.one * scale;

        transform.GetComponentInChildren<Camera>().targetTexture = minimapRenderMaps[playerNmb];
        minimapRender.GetComponent<RawImage>().texture = minimapRenderMaps[playerNmb];
    }

    Bounds CalculateSceneBounds()
    {
        Bounds b = new Bounds();
        foreach (Renderer r in GameObject.FindObjectsOfType<Renderer>())
        {
            b.Encapsulate(r.bounds);
        }
        return b;
    }

}
