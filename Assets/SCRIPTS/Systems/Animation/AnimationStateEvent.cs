using UnityEngine;

public interface ISMBReceiver
{
    void OnAnimationSignal(string functionName, AnimationStateEvent.StateEvent data);
}

public class AnimationStateEvent
{
    [System.Serializable]
    public class StateEvent
    {
        public enum ArgumentType { none, boolType, floatType, intType, stringType, UnityObjectType }

        [SerializeField] private bool m_enabled = true;
        [SerializeField, Range(0f, 1f)] private float m_normalizedTime = 0.5f;
        [SerializeField] private bool m_ForceCallOnExit = true;
        [SerializeField] private bool m_repeatOnLoop = true;
        [SerializeField] private string m_FunctionName = string.Empty;
        [SerializeField] private ArgumentType m_parameterType = ArgumentType.stringType;

        [SerializeField] private bool m_boolParameter;
        [SerializeField] private float m_floatParameter;
        [SerializeField] private int m_intParameter;
        [SerializeField] private string m_stringParameter;
        [SerializeField] private Object m_objectParameter;

        // Public Getters
        public float NormalizedTime => m_normalizedTime;
        public bool RepeatOnLoop => m_repeatOnLoop;
        public bool ForceCallOnExit => m_ForceCallOnExit;
        public string FunctionName => m_FunctionName;
        public bool BoolValue => m_boolParameter;
        public float FloatValue => m_floatParameter;
        public int IntValue => m_intParameter;
        public string StringValue => m_stringParameter;
        public Object ObjectValue => m_objectParameter;

        public void Invoke(ISMBReceiver[] receivers)
        {
            if (!m_enabled || string.IsNullOrEmpty(m_FunctionName) || receivers == null) return;
            for (int i = 0; i < receivers.Length; i++)
            {
                receivers[i]?.OnAnimationSignal(m_FunctionName, this);
            }
        }
    }
}
