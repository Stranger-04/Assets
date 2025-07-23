using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[ExecuteInEditMode]
public class Render3DTexture : MonoBehaviour {

    public Vector3 Size = Vector3.one;
    public Vector3 Offset = Vector3.zero;
    public Vector3Int TextureSize = new Vector3Int(64,64,64);

    private Camera renderCam;

    private RenderTexture renderTexture;

    private Texture3D volumeTex;

    public Material SliceMat;

    public Material BlurMat;

    public bool Blur = false;

    //public List<Texture2D> textureArray = new List<Texture2D>();

    [Range(0,1.0f)]
    public float ClipValueRange = 0;
    private float ClipValue = -0.5f;
    [Tooltip("输出路径")]
    public string OutputPath = "testAsset1.asset";

    private void Update()
    {
        if (SliceMat == null)
        {
            SliceMat = GetComponent<Renderer>().sharedMaterial;
        }
        if (SliceMat != null)
        {
            float min = -Size.y * 0.5f + Offset.y+transform.position.y;
            float max = Size.y * 0.5f + Offset.y +transform.position.y;
            float value = Mathf.Lerp(min, max, ClipValueRange);
            SliceMat.SetFloat("_ClipValue", value);
        }
    }
    /// <summary>
    /// 生成相机
    /// </summary>
    private void NewCam()
    {
        GameObject cam = new GameObject("TempCam");

        cam.transform.position = transform.position + Size.y *0.51f *Vector3.up +Offset;

        Vector3 localEuler = cam.transform.localEulerAngles;

        localEuler.x = 90;
        localEuler.y = 180;

        cam.transform.localEulerAngles = localEuler;

        renderCam = cam.AddComponent<Camera>();

        renderCam.orthographic = true;

        renderCam.orthographicSize = Size.x*0.5f;

        renderCam.nearClipPlane = 0.0001f;
        renderCam.farClipPlane = 100;

        renderCam.clearFlags = CameraClearFlags.SolidColor;

        renderCam.backgroundColor = Color.black;

        renderTexture = CreateRT(TextureSize.x, TextureSize.z);

        renderCam.targetTexture = renderTexture;

    }

    public void StartRender()
    {
        NewCam();

        volumeTex = new Texture3D(TextureSize.x, TextureSize.y, TextureSize.z,TextureFormat.RFloat,false);

        RenderSlice();
    }

    public void RenderSlice()
    {

        //渲染范围
        Vector3 center = transform.position;
        float min = -Size.y * 0.5f + Offset.y + center.y;
        float max = Size.y * 0.5f + Offset.y + center.y;

        ClipValue = min;

        Color[] colors = new Color[TextureSize.x * TextureSize.y * TextureSize.z];

        int layerCount = TextureSize.x * TextureSize.z;

        for (int layer = 0; layer < TextureSize.y; layer++)
        {
            renderCam.Render();

            ClipValue += (max - min) / TextureSize.y;

            float progress = (float)layer / TextureSize.y;

            bool isCancel = EditorUtility.DisplayCancelableProgressBar("正在执行..",string.Format("生成3DTexture中... {0:f2}%", progress*100), progress);

            if (SliceMat != null)SliceMat.SetFloat("_ClipValue", ClipValue);

            Texture2D sliceTex = RenderTexture2Texture2D(renderTexture);


            int index = 0;
            //拷贝颜色
            for (int z = 0; z < TextureSize.z; z++)
                for (int x = 0; x < TextureSize.x; x++)
                {
                    //XY ->XZ
                    index = x + z * TextureSize.y * TextureSize.x+layer* TextureSize.x;
                    colors[index] = sliceTex.GetPixel(x, z);
                }
            //渲染完成或者取消时关闭进度条
            if (layer >= TextureSize.y - 1 || isCancel)
            {
                EditorUtility.ClearProgressBar();

                if (isCancel)
                {
                    DestroyImmediate(renderCam.gameObject);
                    renderTexture.Release();

                    return;
                }
            }
        }

        volumeTex.SetPixels(colors);
        volumeTex.Apply();
        if (Blur)
        {
            volumeTex = BlurTexture(volumeTex);
        }
        string filePath = "Assets/"+ OutputPath;
        try
        {
            AssetDatabase.DeleteAsset(filePath);
            AssetDatabase.CreateAsset(volumeTex, filePath);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
        }
        DestroyImmediate(renderCam.gameObject);
        renderTexture.Release();
        renderTexture = null;
        UnityEditor.AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    /// <summary>
    /// Renderer转成Texture2D
    /// </summary>
    /// <param name="rt"></param>
    /// <returns></returns>
    public Texture2D RenderTexture2Texture2D(RenderTexture rt)
    {
        RenderTexture preRT = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = preRT;
        return tex;
    }

    public void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(this.transform.position+Offset, Size);
    }

    public void OnDestory()
    {
        renderTexture.Release();
    }

    public Texture3D BlurTexture(Texture3D tex)
    {
        for (int i = 0; i < 2; i++)
        {
            Color[] colors = new Color[TextureSize.x * TextureSize.y * TextureSize.z];

            int index = 0;

            for (int layer = 0; layer < TextureSize.y; layer++)
            {
                BlurMat.SetTexture("_VolumeTex", tex);
                BlurMat.SetFloat("_offset", layer * 1.0f / TextureSize.y);
                Debug.Log(layer * 1.0f / TextureSize.y);

                RenderTexture rt = new RenderTexture(TextureSize.x, TextureSize.z, 24, RenderTextureFormat.ARGB32);

                Graphics.Blit(null, rt, BlurMat);

                Texture2D sliceTex = RenderTexture2Texture2D(rt);

                //拷贝颜色
                for (int z = 0; z < TextureSize.z; z++)
                    for (int x = 0; x < TextureSize.x; x++)
                    {
                        //XY ->XZ
                        index = x + z * TextureSize.y * TextureSize.x + layer * TextureSize.x;
                        colors[index] = sliceTex.GetPixel(x, z);
                    }
            }

            tex.SetPixels(colors);
            tex.Apply();
        }
        return tex;
    }

    private RenderTexture CreateRT(int width, int height)
    {
        RenderTexture rt = new RenderTexture(width, height, 16);
        rt.format = RenderTextureFormat.ARGBFloat;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.filterMode = FilterMode.Point;
        rt.Create();
        return rt;
    }
}
