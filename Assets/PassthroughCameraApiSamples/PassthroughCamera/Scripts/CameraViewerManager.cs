using System.Collections;
using PassthroughCameraSamples;
using UnityEngine;
using UnityEngine.UI;

public class CameraViewerManager : MonoBehaviour
{
    [SerializeField] private WebCamTextureManager m_webCamTextureManager;

    [SerializeField] private RawImage m_image;

    [SerializeField] private Text m_debugText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private IEnumerator Start()
    {
        while (m_webCamTextureManager.WebCamTexture==null)
        {
            yield return null;
        }
        m_image.texture = m_webCamTextureManager.WebCamTexture;
    }
}
