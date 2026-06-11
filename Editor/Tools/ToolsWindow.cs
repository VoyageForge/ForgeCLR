using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VoyageForge.ForgeCLR.Editor.Tools
{
    public class ToolsWindow : EditorWindow
    {
        [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

        [MenuItem("VoyageForge/ForgeCLR/Tools %&s" )]
        public static void ShowExample()
        {
            ToolsWindow wnd = GetWindow<ToolsWindow>();
            wnd.titleContent = new GUIContent("Tools");
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