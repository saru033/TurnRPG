#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using TurnRPG.SkillSystem;

namespace TurnRPG.EditorScripts
{
    [CustomPropertyDrawer(typeof(SkillEffect), true)]
    public class SkillEffectDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // 헤더 버튼 높이
            float height = EditorGUIUtility.singleLineHeight + 4f; 
            
            // 만약 선택된 클래스가 있다면 그 안의 변수들(파라미터)을 그릴 높이도 추가 확보
            if (property.managedReferenceValue != null)
            {
                height += EditorGUI.GetPropertyHeight(property, true); 
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 타입 선택용 버튼 (드롭다운) 영역 잡기
            Rect buttonRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            
            // 현재 선택된 타입 이름 가져오기
            string typeName = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(typeName)) 
                typeName = "⭐ 클릭하여 [이펙트 종류] 선택";
            else 
                typeName = "현재 타입: " + typeName.Split('.').Last().Split(' ').Last(); 

            // 버튼을 클릭했을 때
            if (GUI.Button(buttonRect, typeName, EditorStyles.popup))
            {
                GenericMenu menu = new GenericMenu();
                
                // 1. 비우기 항목
                menu.AddItem(new GUIContent("삭제 (Empty)"), false, () =>
                {
                    property.serializedObject.Update();
                    property.managedReferenceValue = null;
                    property.serializedObject.ApplyModifiedProperties();
                });
                menu.AddSeparator("");

                // 2. SkillEffect를 물려받은 모든 스크립트 자동 감지하여 메뉴에 추가
                var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .Where(p => typeof(SkillEffect).IsAssignableFrom(p) && !p.IsAbstract && p.IsClass);

                foreach (var type in types)
                {
                    menu.AddItem(new GUIContent(type.Name), false, () =>
                    {
                        property.serializedObject.Update();
                        property.managedReferenceValue = Activator.CreateInstance(type);
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }
                menu.ShowAsContext();
            }

            // 만약 뭐라도 선택되어 있다면 변수들 그리기
            if (property.managedReferenceValue != null)
            {
                position.y += EditorGUIUtility.singleLineHeight + 4f;
                EditorGUI.PropertyField(position, property, GUIContent.none, true);
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif
