using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[ExecuteInEditMode]
public class ImageBase : MonoBehaviour {

	public Material mMaterial = null;

	public string shaderName = "Hidden/Cloud";
	// Use this for initialization
	protected virtual void Awake () {
		if(mMaterial==null)
		mMaterial = new Material(Shader.Find(shaderName));
	}
	protected virtual void OnRenderImage(RenderTexture s,RenderTexture d)
	{
		if(mMaterial!=null)
		{

			Graphics.Blit(s,d,mMaterial);
		}
	}
}
