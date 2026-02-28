using UnityEngine;

public class Notes : MonoBehaviour
{
    // Start is called before the first frame update
    //for leaving notes
    [TextArea(5, 5)]
    public string Note = "";
}
