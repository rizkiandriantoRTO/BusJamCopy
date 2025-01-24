using UnityEngine;

public class MaterialCharacterController : MonoBehaviour
{

    public Character character { get; private set; }

    public SkinnedMeshRenderer SkinnedMeshRenderer;

    public MeshRenderer busMeshRenderer;


    private void Awake()
    {
        character = GetComponent<Character>();
        SkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    private void Update()
    {

    }

    public void SetCharacterMaterial(Material material)
    {
        if (this.gameObject.tag == "Bus")
        {
            busMeshRenderer.sharedMaterial = material;
        }
        else
        {
            // SkinnedMeshRenderer.material = material;
        }
    }
}
