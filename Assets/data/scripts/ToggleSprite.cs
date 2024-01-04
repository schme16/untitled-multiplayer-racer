using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleSprite : MonoBehaviour
{

    private Sprite spriteA;
    public Sprite spriteB;
    public float interval = 1f;
    private SpriteRenderer spriteRenderer;
    private float time = 0;
    
    // Start is called before the first frame update
    void Start()
    {
        spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        spriteA = spriteRenderer.sprite;
    }

    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime;
        if (time >= interval)
        {
            if (spriteRenderer.sprite == spriteA)
            {
                spriteRenderer.sprite = spriteB;
            }
            else
            {
                spriteRenderer.sprite = spriteA;
            }

            time = 0;
        }
    }
}
