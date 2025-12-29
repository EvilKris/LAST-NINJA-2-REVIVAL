using UnityEngine;
using UnityEngine.InputSystem;

public class MovementAndInputController : MonoBehaviour
{

    private Vector3 _mouseWorldVector = Vector3.zero;
    private Vector2 _userVector = Vector2.zero;
    private Vector2 _userLookVector = Vector2.zero;
    private BaseCreature _baseCreature;
    private Rigidbody _rb;
    private Animator _animator;
    private int _layerMask;

    readonly string isRunning = "isRunningBool"; //must match with the same name in the Animator 
    readonly string anim_xAxis = "Input_XFloat"; //x, y
    readonly string anim_yAxis = "Input_YFloat";

    private void Awake()
    {
        _baseCreature = GetComponentInParent<BaseCreature>();
        Debug.Assert(_baseCreature != null, $"You need BaseCreature or a derivative on { this.name}!");
        _rb = _baseCreature.rb;
        _animator = _baseCreature.animator;
    }

    private void Start()
    {
        _layerMask = LayerMask.GetMask("Floor"); //label anything that is the ground with Floor
    }

   
    void FixedUpdate()
    {
        //Update = movement, input control (needs deltaTime though) 
        //Fixed update = physics (triggers, collisions) 
        //Late update = camera 
        if (!_baseCreature.isAbleToMove) return;

        RotateThisObject();     

        Move();
    }

    #region Callbacks From Input
    public void OnMoveCallBack(InputAction.CallbackContext context)
    {
        //Called from Input Manager Component Event-  Activates on Press WASD
        _userVector = context.ReadValue<Vector2>();
        _animator.SetFloat(anim_xAxis, _userVector.x);
        _animator.SetFloat(anim_yAxis, _userVector.y);
    }

    public void OnLookCallBack(InputAction.CallbackContext context)
    {  
        _mouseWorldVector = new Vector3(context.ReadValue<Vector2>().x, context.ReadValue<Vector2>().y, 0f);
    }
    #endregion

    #region movement

    private void Move()
    {
        _rb.linearVelocity = transform.TransformDirection(_baseCreature.movementSpeed * new Vector3(_userVector.x, 0, _userVector.y));
        _animator.SetBool(isRunning, true);
    }

    private void RotateThisObject()
    {
        Ray ray = Camera.main.ScreenPointToRay(_mouseWorldVector);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, _layerMask))
        {
            Debug.Log("jaldksjkldasjkl");
            if (Vector3.Distance(transform.position, hit.point) < 0.1f) return;

            Vector3 eulerRotation = Quaternion.LookRotation(hit.point - transform.position).eulerAngles;
            eulerRotation.x = eulerRotation.z = 0;//remove values from x and z since we only need y
            //60 is just a random scalar value
            _rb.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(eulerRotation), Vector3.Distance(transform.position, hit.point) * 60f * Time.deltaTime);
        }
    }




    #endregion
}
