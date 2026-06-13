using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VoyageForge.ForgeCLR.Editor.Tools
{
    public class TestsWindow: EditorWindow
    {
        [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

        [MenuItem("VoyageForge/ForgeCLR/Tests %&d" )]
        public static void ShowExample()
        {
            TestsWindow wnd = GetWindow<TestsWindow>();
            wnd.titleContent = new GUIContent("Tests");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.CloneTree();
            
            labelFromUXML.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            labelFromUXML.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
            
            root.Add(labelFromUXML);
        }
    }
}