﻿/*This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.

The Original Code is the TSOClient.

The Initial Developer of the Original Code is
ddfczm. All Rights Reserved.

Contributor(s): ______________________________________.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using Microsoft.Xna.Framework;
using TSOClient.LUI;
using TSOClient.Code.Utils;
using TSOClient.Code.UI.Controls;

namespace TSOClient.Code.UI.Framework.Parser
{
    public class UIScript
    {


        /// <summary>
        /// Nodes which represent functions
        /// </summary>
        private static string[] FUNCTIONS = new string[] { "DefineString", "DefineImage", "AddButton", "SetControlProperties", "AddText", "AddTextEdit", "AddSlider", "AddListBox", "AddFormatedText" };

        private Dictionary<string, string> Strings;
        private Dictionary<string, Texture2D> Textures;
        private Dictionary<string, UIElement> Components;
        private Dictionary<string, object> NamedObjects;
        private Dictionary<string, UINode> NodesByID;
        private Dictionary<string, UINode> ControlSettings;
        private GraphicsDevice gd;

        private UIContainer target;
        private Type targetType;

        public UIScript(GraphicsDevice gd, UIContainer target)
        {
            this.gd = gd;
            this.target = target;
            this.targetType = target.GetType();
            Strings = new Dictionary<string, string>();
            Textures = new Dictionary<string, Texture2D>();
            Components = new Dictionary<string, UIElement>();
            NodesByID = new Dictionary<string, UINode>();
            NamedObjects = new Dictionary<string, object>();
            ControlSettings = new Dictionary<string, UINode>();
        }


        public void LinkMembers(UIContainer target, bool addUIElements)
        {
            var fields = target.GetType().GetProperties();
            foreach (var field in fields)
            {
                if (NamedObjects.ContainsKey(field.Name))
                {
                    var value = NamedObjects[field.Name];
                    field.SetValue(target, value, new object[]{});

                    if (value is UIElement && addUIElements)
                    {
                        var uiValue = (UIElement)value;
                        if (uiValue.Parent != null)
                        {
                            uiValue.Parent.Remove(uiValue);
                        }
                        target.Add(uiValue);
                    }
                }
            }
        }


        public object this[string id]
        {
            get { 
                if (NamedObjects.ContainsKey(id)) return NamedObjects[id];
                return null;
            }
        }

        public T Create<T>(string id)
        {
            var instance = Activator.CreateInstance<T>();
            if (ControlSettings.ContainsKey(id))
            {
                DoSetControlProperties(instance, ControlSettings[id]);
            }
            return instance;
        }

        public T Create<T>(string id, object param)
        {
            var instance = Activator.CreateInstance(typeof(T), new object[] { param });
            if (ControlSettings.ContainsKey(id))
            {
                DoSetControlProperties(instance, ControlSettings[id]);
            }
            return (T)instance;
        }

        public object GetControlProperty(string id)
        {
            return GetControlProperty(target, id);
        }

        public object GetControlProperty(object target, string id)
        {
            if (ControlSettings.ContainsKey(id))
            {
                var props = DoGetControlProperties(target, ControlSettings[id]);
                if (props.Keys.Count == 1)
                {
                    return props[props.Keys.First()];
                }
                return props;
            }
            return null;
            //DoGetControlProperties
        }

        /// <summary>
        /// Handles AddText nodes in a UIScript
        /// This method will:
        ///  * Create a label
        ///  * Assign all the properties from the UIScript
        ///  * Add to the display list
        ///  * Wire up against any members with the same name in the target class
        /// </summary>
        /// <param name="node"></param>
        public void AddText(UINode node)
        {
            var label = new UILabel();
            DoSetControlProperties(label, node);
            target.Add(label);
            WireUp(node.ID, label);
        }


        /// <summary>
        /// Handles AddButton nodes in a UIScript.
        /// This method will:
        ///  * Create a button,
        ///  * Assign all the properties from the UIScript
        ///  * Add to the display list
        ///  * Wire up against any members with the same name in the target class
        /// </summary>
        /// <param name="node"></param>
        public void AddButton(UINode node)
        {
            UIButton btn = null;
            if (node.Attributes.ContainsKey("image"))
            {
                var txKey = node.Attributes["image"];
                if (Textures.ContainsKey(txKey))
                {
                    btn = new UIButton(Textures[txKey.ToLower()]);
                }
                else
                {
                    btn = new UIButton();
                }
            }
            else
            {
                btn = new UIButton();
            }
            Components.Add(node.ID, btn);
            btn.ID = node.ID;

            DoSetControlProperties(btn, node);
            target.Add(btn);
            WireUp(node.ID, btn);
        }


        public void AddSlider(UINode node)
        {
            UISlider slider = new UISlider();
            Components.Add(node.ID, slider);
            slider.ID = node.ID;

            DoSetControlProperties(slider, node);
            target.Add(slider);
            WireUp(node.ID, slider);
        }

        public void AddListBox(UINode node)
        {
            UIListBox list = new UIListBox();
            Components.Add(node.ID, list);
            list.ID = node.ID;

            DoSetControlProperties(list, node);
            target.Add(list);
            WireUp(node.ID, list);
        }

        

        public void AddTextEdit(UINode node)
        {
            UITextEdit textEdit = new UITextEdit();
            Components.Add(node.ID, textEdit);
            textEdit.ID = node.ID;

            DoSetControlProperties(textEdit, node);
            target.Add(textEdit);
            WireUp(node.ID, textEdit);
        }

        public void AddFormatedText(UINode node)
        {
            UITextEdit textEdit = new UITextEdit();
            Components.Add(node.ID, textEdit);
            textEdit.ID = node.ID;

            DoSetControlProperties(textEdit, node);

            textEdit.Mode = UITextEditMode.ReadOnly;

            target.Add(textEdit);
            WireUp(node.ID, textEdit);
        }




        public void SetControlProperties(UINode node)
        {

            /** If the component is already setup, change the settings **/
            if (Components.ContainsKey(node.ID))
            {
                DoSetControlProperties(Components[node.ID], node);
            }
            else if (NodesByID.ContainsKey(node.ID))
            {
                /** It could be another node in this UIScript, try find and merge the settings **/
                var childNode = NodesByID[node.ID];
                childNode.AddAtts(node.Attributes);
            }
            else
            {
                /** Might be a field already created on the UI container **/
                var prop = targetType.GetProperty(node.ID);
                if (prop != null)
                {
                    var propValue = prop.GetValue(target, new object[] { });
                    if (propValue != null && propValue is UIElement)
                    {
                        DoSetControlProperties(propValue, node);
                    }
                }
            }

            ControlSettings[node.ID] = node;
        }

        /// <summary>
        /// Applys the various settings in the UINode to the control
        /// provided
        /// </summary>
        /// <param name="control"></param>
        /// <param name="node"></param>
        private void DoSetControlProperties(object control, UINode node)
        {
            if (control == null) { return; }

            /** Get UIAttribute decorations **/
            var atts = GetTypeFields(control.GetType());
            
            foreach (var att in node.Attributes)
            {
                if (atts.ContainsKey(att.Key))
                {
                    var uiAtt = atts[att.Key];
                    var value = GetAtt(node, att.Key, uiAtt);
                    if (value != null) uiAtt.Field.SetValue(control, value, new object[] { });
                }
            }
        }


        /// <summary>
        /// Applys the various settings in the UINode to the control
        /// provided
        /// </summary>
        /// <param name="control"></param>
        /// <param name="node"></param>
        private Dictionary<string, object> DoGetControlProperties(object control, UINode node)
        {
            if (control == null) { return null; }

            /** Get UIAttribute decorations **/
            var atts = GetTypeFields(control.GetType());
            var result = new Dictionary<string, object>();

            foreach (var att in node.Attributes)
            {
                if (atts.ContainsKey(att.Key))
                {
                    var uiAtt = atts[att.Key];
                    var value = GetAtt(node, att.Key, uiAtt);

                    result.Add(att.Key, value);
                }
            }
            return result;
        }


        /// <summary>
        /// Gets 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="name"></param>
        /// <param name="converter"></param>
        /// <returns></returns>
        private object GetAtt(UINode node, string name, UIAttField field)
        {
            if (field.Parser != null)
            {
                var instance = (UIAttributeParser)Activator.CreateInstance(field.Parser);
                instance.ParseAttribute(node);
                return instance;
            }

            switch (field.Converter)
            {
                case UIAttributeType.Color:
                    return node.GetColor(name);
                case UIAttributeType.Point:
                    return node.GetPoint(name);
                case UIAttributeType.Texture:
                    if (Textures.ContainsKey(node[name].ToLower())) return Textures[node[name].ToLower()];
                    else return null;
                case UIAttributeType.Vector2:
                    return node.GetVector2(name);
                case UIAttributeType.StringTable:
                    if (Strings.ContainsKey(node[name].ToLower())) return Strings[node[name].ToLower()];
                    else return "";
                case UIAttributeType.String:
                    return node[name];
                case UIAttributeType.Integer:
                    return Int32.Parse(node[name]);
                case UIAttributeType.Boolean:
                    return node[name] == "1";
            }
            return null;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        public void DefineString(UINode node)
        {
            var stringValue = GameFacade.Strings[node["stringDir"], node["stringTable"], node["stringIndex"]];
            Strings.Add(node.ID.ToLower(), stringValue);
            WireUp(node.ID, stringValue);
        }

        /// <summary>
        /// Handles the DefineImage nodes in the UIScript
        /// </summary>
        /// <param name="node"></param>
        public void DefineImage(UINode node)
        {
            var assetID = node["assetID"];
            var assetNum = ulong.Parse(assetID.Substring(2), NumberStyles.HexNumber);
            try
            {
                var texture = UIElement.GetTexture(assetNum);

                Textures.Add(node.ID.ToLower(), texture);
                WireUp(node.ID, texture);
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("Failed to load texture: " + assetID);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public string GetString(string id)
        {
            if (Strings.ContainsKey(id)) return Strings[id];
            else return "";
        }

        

        /// <summary>
        /// Tries to assign a value created by the UIScript to a
        /// property in the target class
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        private void WireUp(string id, object value)
        {
            NamedObjects.Add(id, value);

            var prop = targetType.GetProperty(id);
            if (prop != null)
            {
                prop.SetValue(target, value, new object[] { });
            }
        }



        public void Parse(string path)
        {
            var parser = new UIScriptParser();
            parser.Setup();
            using (var reader = File.OpenText(path))
            {
                var didParse = parser.Parse(reader);
                if (didParse)
                {
                    var children = (List<UINode>)parser.program;
                    this.Process((UIGroup)children.First());
                }
            }
        }


        public void Process(UIGroup content)
        {
            /** Pre-Process **/
            PreProcessUIGroup(content, new Dictionary<string, string>());
            ProcessUIGroup(content);
        }


        private void PreProcessUIGroup(UIGroup content, Dictionary<string, string> shared)
        {
            var sharedNode = content.SharedProperties;
            if (sharedNode != null)
            {
                foreach (var att in sharedNode.Attributes)
                {
                    shared.Add(att.Key, att.Value);
                }
            }

            var type = typeof(UIScript);
            var funcSig = new Type[] { typeof(UINode) };

            foreach (var child in content.Children)
            {
                child.AddAtts(shared);
                if (child is UIGroup)
                {
                    PreProcessUIGroup((UIGroup)child, CollectionUtils.Clone(shared));
                }

                if(child.ID != null && child.Name != "SetControlProperties"){
                    NodesByID[child.ID] = child;
                }
            }
        }

        private void ProcessUIGroup(UIGroup content)
        {

            var type = typeof(UIScript);
            var funcSig = new Type[] { typeof(UINode) };

            foreach (var child in content.Children)
            {
                //child.AddAtts(shared);
                if (child is UIGroup)
                {
                    ProcessUIGroup((UIGroup)child);
                }
                else
                {
                    if (child.Name != null && FUNCTIONS.Contains(child.Name))
                    {
                        var m = type.GetMethod(child.Name, funcSig);
                        m.Invoke(this, new object[] { child });
                    }
                }
            }
        }

        private Dictionary<Type, Dictionary<string, UIAttField>> FieldCache
            = new Dictionary<Type, Dictionary<string, UIAttField>>();

        /// <summary>
        /// Gets a map of string => UIAttField.
        /// The map indicates what UIScript attribute name e.g. "position"
        /// maps to in the target type via a [UIAttribute] decoration
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public Dictionary<string, UIAttField> GetTypeFields(Type type)
        {
            if (FieldCache.ContainsKey(type))
            {
                return FieldCache[type];
            }


            PropertyInfo[] infos =
                type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            var idMap = new Dictionary<string, UIAttField>();

            foreach (var field in infos)
            {
                var atts = field.GetCustomAttributes(typeof(UIAttribute), true);
                if (atts.Length > 0)
                {
                    var convertType = UIAttributeType.Unknown;
                    var fieldType = field.PropertyType;

                    if (fieldType.IsAssignableFrom(typeof(Texture2D)))
                    {
                        convertType = UIAttributeType.Texture;
                    }
                    else if (fieldType.IsAssignableFrom(typeof(Point)))
                    {
                        convertType = UIAttributeType.Point;
                    }
                    else if (fieldType.IsAssignableFrom(typeof(Vector2)))
                    {
                        convertType = UIAttributeType.Vector2;
                    }
                    else if (fieldType.IsAssignableFrom(typeof(Int32)))
                    {
                        convertType = UIAttributeType.Integer;
                    }
                    else if (fieldType.IsAssignableFrom(typeof(Boolean)))
                    {
                        convertType = UIAttributeType.Boolean;
                    }
                    else if (fieldType.IsAssignableFrom(typeof(Color)))
                    {
                        convertType = UIAttributeType.Color;
                    }
                    else if (fieldType.IsAssignableFrom(typeof(string)))
                    {
                        convertType = UIAttributeType.String;
                    }

                    foreach (UIAttribute att in atts)
                    {
                        idMap.Add(att.Name, new UIAttField
                        {
                            Field = field,
                            Converter = att.DataType == UIAttributeType.Unknown ? convertType : att.DataType,
                            Parser = att.Parser
                        });
                    }

                }
            }

            FieldCache.Add(type, idMap);
            return idMap;
        }









        public static Color ParseRGB(string rgb)
        {
            if (rgb.StartsWith("("))
            {
                rgb = rgb.Substring(1);
            }
            if (rgb.EndsWith(")"))
            {
                rgb = rgb.Substring(0, rgb.Length - 1);
            }

            var split = rgb.Split(new char[] { ',' });
            return new Color(
                byte.Parse(split[0]),
                byte.Parse(split[1]),
                byte.Parse(split[2])
            );
        }
    }


    public enum UIAttributeType
    {
        Point,
        Texture,
        Vector2,
        Unknown,
        StringTable,
        String,
        Integer,
        Boolean,
        Color
    }

    public class UIAttField
    {
        public PropertyInfo Field;
        public UIAttributeType Converter;
        public Type Parser;
    }

}
