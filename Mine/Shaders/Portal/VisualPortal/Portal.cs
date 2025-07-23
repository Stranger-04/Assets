using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PortalProject
{

    public class Portal : MonoBehaviour
    {
        public Portal targetPortal;

        public Renderer screen;
        public Camera myCamera;

        public RenderTexture rt;
        public RenderTexture lastRt;

        [HideInInspector]
        public MeshFilter mesh;

        float origScalez; 

        private void Awake()
        {
            screen = GetComponentInChildren<Renderer>();
            origScalez = screen.transform.localScale.z;
            mesh = screen.GetComponent<MeshFilter>();
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += PostCameraRendering;
        }


        #region ͼ��

        void CreateTexture()
        {
            if (rt == null || rt.width != Screen.width || rt.height != Screen.height)
            {
                if (rt != null)
                    rt.Release();
                if(lastRt != null) 
                    lastRt.Release();
                //����depth��Ҫ���ó�24��0��ʱ����Ⱦ����ȷ
                rt = new RenderTexture(Screen.width, Screen.height, 24, UnityEngine.Experimental.Rendering.DefaultFormat.LDR);
                lastRt = new RenderTexture(Screen.width, Screen.height, 24, UnityEngine.Experimental.Rendering.DefaultFormat.LDR);
                myCamera.targetTexture = rt;

                targetPortal.screen.material.mainTexture = rt;
            }

        }
        static bool VisibleFromCamera(Renderer renderer,Camera camera)
        {
            Plane[] ps = GeometryUtility.CalculateFrustumPlanes(camera);
            return GeometryUtility.TestPlanesAABB(ps, renderer.bounds);
        }
        //ѭ������
        public int recursionLimit = 5;

        
        private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            //Debug.Log(camera.name);
            //todo�����ܼ�����жϣ�����������Ļ������camera�л���˸������ԭ��
            //if (camera != Camera.main)
                //return;

            if(!VisibleFromCamera(targetPortal.screen,Camera.main))
                return;

            #region ver1.0

            //screen.enabled = false;
            //CreateTexture();
            //var m = transform.localToWorldMatrix * targetPortal.transform.worldToLocalMatrix * Camera.main.transform.localToWorldMatrix;
            //myCamera.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
            //SetNearClipPlane();
            //UniversalRenderPipeline.RenderSingleCamera(context, myCamera);
            //screen.enabled = true;

            #endregion

            CreateTexture();
            int startIndex = -1;
            Matrix4x4 localToWorldMatrix = Camera.main.transform.localToWorldMatrix;
            Matrix4x4[] matrices = new Matrix4x4[recursionLimit];
            //�Ƚ�����ŵ������λ�ã�ѭ����ͨ���������Ƿ���ͨ���˴����ſ������洫����
            myCamera.projectionMatrix = Camera.main.projectionMatrix;
            for (int i = 0; i < recursionLimit; i++)
            {
                if (i>0&&!CameraUtility.BoundsOverlap(mesh, targetPortal.mesh, myCamera))
                    break;
                //������ȡѭ�����ݺ��Զ���ľ���
                localToWorldMatrix = transform.localToWorldMatrix * targetPortal.transform.worldToLocalMatrix * localToWorldMatrix;
                //��Զ�����Ⱦ
                matrices[recursionLimit - i - 1] = localToWorldMatrix;

                myCamera.transform.SetPositionAndRotation(localToWorldMatrix.GetColumn(3), localToWorldMatrix.rotation);
                startIndex = recursionLimit - i - 1;
            }


            //screen.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            screen.gameObject.SetActive(false);
            
            float mask = (startIndex== recursionLimit-1)?0:1;
            float d = 1f/(recursionLimit - startIndex + 1);
            //ǰ����������mask
            d *= 1.5f;

            for(int i = startIndex;i<recursionLimit;i++)
            {
                
                myCamera.transform.SetPositionAndRotation(matrices[i].GetColumn(3), matrices[i].rotation);
                //���ý��ü�ƽ��
                SetNearClipPlane();
                

                //todo:ʹ������rendertexture������Ⱦ���̳���ʹ�õ�camera.render��ֻҪһ��rendertexture������urp�汾������camera.render����ֻ��һ��ʱ�޷��ݹ���Ⱦ������ԭ��
                if (myCamera.targetTexture == rt)
                {
                    myCamera.targetTexture = lastRt;
                    targetPortal.screen.material.SetFloat("mask", mask);
                    UniversalRenderPipeline.RenderSingleCamera(context, myCamera);
                    targetPortal.screen.material.mainTexture = lastRt;
                    mask = Math.Max(0, mask - d);
                }
                else
                {
                    myCamera.targetTexture = rt;
                    targetPortal.screen.material.SetFloat("mask", mask);
                    UniversalRenderPipeline.RenderSingleCamera(context, myCamera);
                    targetPortal.screen.material.mainTexture = rt;
                    mask = Math.Max(0, mask - d);
                }

                //UniversalRenderPipeline.RenderSingleCamera(context, myCamera);
                //myCamera.Render();


                //UnityEditor.EditorApplication.isPaused = true;


            }

            //screen.shadowCastingMode = ShadowCastingMode.On;
            screen.gameObject.SetActive(true);


        }
        private void PostCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != Camera.main)
                return;

            for(int i = 0;i<trackedTravellers.Count;i++)
            {
                UpdateSliceParams(trackedTravellers[i]);
            }

            ProtectScreenFromClipping();
        }
        void ProtectScreenFromClipping()
        {
            //���ü�ƽ��ĸ߶�
            float halfHeight = Camera.main.nearClipPlane * Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * Camera.main.aspect;
            //�����ü�ƽ��Ľǵľ���
            float dstToNearClipPlaneCorner = new Vector3(halfWidth, halfHeight, Camera.main.nearClipPlane).magnitude;

            Transform screenT = screen.transform;
            //������ŵĳ�����ͬ
            bool camFacingSameDirAsPortal = Vector3.Dot(transform.forward, transform.position - Camera.main.transform.position) > 0;

            screenT.localScale = new Vector3(dstToNearClipPlaneCorner, screenT.localScale.y, screenT.localScale.z);

            //ƫ��λ�ã�ʹ���޸Ĵ�С�������ҽ���������ԭ��λ��
            Vector3 lp = screenT.localPosition;
            lp.z = dstToNearClipPlaneCorner * (camFacingSameDirAsPortal ? 0.5f : -0.5f);
            screenT.localPosition = lp;
        }

        void SetNearClipPlane()
        {
            Transform clipPlane = transform;
            int dot = System.Math.Sign(Vector3.Dot(clipPlane.forward, transform.position - myCamera.transform.position));

            //��ȡ����������ռ��е�λ��
            Vector3 camSpacePos = myCamera.worldToCameraMatrix.MultiplyPoint(clipPlane.position);
            //��ȡ��������������dot���÷���
            Vector3 campSpaceNormal = myCamera.worldToCameraMatrix.MultiplyVector(clipPlane.forward) * dot;
            //��ȡ�����������Ļƽ��Ĵ�ֱ����
            float camSpaceDst = -Vector3.Dot(camSpacePos, campSpaceNormal);

            //��ʾ�������camSpaceDst����ķ���ΪcampSpaceNormal��ƽ��
            Vector4 clipPlaneCameraSpace = new Vector4(campSpaceNormal.x, campSpaceNormal.y, campSpaceNormal.z, camSpaceDst);

            //�Զ���ͶӰ���󣬽�����Ĳü�������Ϊ���ü�ƽ��
            myCamera.projectionMatrix = myCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);

        }

        #endregion

        #region ����
        List<PortalTraveller> trackedTravellers = new List<PortalTraveller>();

        void LateUpdate()
        {
            for (int i = 0; i < trackedTravellers.Count; i++)
            {
                var traveller = trackedTravellers[i];
                Transform travellerT = traveller.transform;

                Vector3 offsetFromPortal = travellerT.position - transform.position;
                int portalSide = System.Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
                int portalSideOld = System.Math.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));
                var m = targetPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerT.localToWorldMatrix;
                traveller.previousOffsetFromPortal = offsetFromPortal; 
                if(portalSide!=portalSideOld)
                {
                    //�͸սӴ�ʱ����ͬһ�࣬˵�������˴�����
                    //��¡�ķŵ�ԭ���ĵط�
                    var oldPos = travellerT.position;
                    var oldRot = travellerT.rotation;
                    traveller.Teleport(transform, targetPortal.transform, m.GetColumn(3), m.rotation);
                    traveller.cloneObj.transform.SetPositionAndRotation(oldPos, oldRot);
                    //��Ϊontriggerenter/exit���ܱ�֤����һ֡�������ã���������fixedupdate�ĵ��ã���������Ҫ�ֶ�����
                    targetPortal.OnTravellerEnterPortal(traveller);
                    trackedTravellers.RemoveAt(i);
                    i--;

                }
                else
                {
                    //��¡�ķŵ������λ��
                    traveller.cloneObj.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                }
            }
        }

        void OnTravellerEnterPortal(PortalTraveller traveller)
        {
            if(!trackedTravellers.Contains(traveller))
            {
                traveller.EnterPortalThreshold();
                traveller.previousOffsetFromPortal = traveller.transform.position - transform.position;
                trackedTravellers.Add(traveller);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var traveller = other.GetComponent<PortalTraveller>(); 
            if(traveller != null)
            {
                Debug.Log("enter");
                OnTravellerEnterPortal(traveller);

            }
        }
        private void OnTriggerExit(Collider other)
        {
            var traveller = other.GetComponent<PortalTraveller>();
            if(traveller!=null&&trackedTravellers.Contains(traveller))
            {
                Debug.Log("exit");
                traveller.ExitPortalThreshold();
                trackedTravellers.Remove(traveller);
                
            }
        }

        int SideOfPortal(Vector3 pos)
        {
            Vector3 offsetFromPortal = pos - transform.position;
            return System.Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
        }

        /// <summary>
        /// ������Ҫ���͵�����Ĳ�������
        /// </summary>
        /// <param name="traveller"></param>

        void UpdateSliceParams(PortalTraveller traveller)
        {
            int side = SideOfPortal(traveller.transform.position);
            Vector3 sliceNormal = transform.forward*(-side);
            Vector3 cloneSliceNormal = targetPortal.transform.forward*side;

            Vector3 slicePos = transform.position;
            Vector3 cloneSlicePos = targetPortal.transform.position;

            for(int i = 0;i<traveller.originalMaterials.Length;i++)
            {
                traveller.originalMaterials[i].SetVector("sliceCenter", slicePos);
                traveller.originalMaterials[i].SetVector("sliceNormal", sliceNormal);

                traveller.cloneMaterials[i].SetVector("sliceCenter", cloneSlicePos);
                traveller.cloneMaterials[i].SetVector("sliceNormal", cloneSliceNormal);

            }



        }

        #endregion

        

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            RenderPipelineManager.beginCameraRendering -= PostCameraRendering;
            if (rt != null) rt.Release();
        }

    }

}