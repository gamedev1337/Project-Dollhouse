﻿/*This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.

The Original Code is the TSOClient.

The Initial Developer of the Original Code is
Mats 'Afr0' Vederhus. All Rights Reserved.

Contributor(s): ______________________________________.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TSOClient.Lot;
using TSOClient.Code.UI.Framework;
using TSOClient.Code.UI.Model;
using TSOClient.LUI;
using TSOClient.Code;
using TSOClient.Code.UI.Controls;
using TSO.Common.rendering.framework.io;
using TSO.Common.rendering.framework.model;
using TSO.Common.rendering.framework;
using TSOClient.Code.Utils;
using System.Diagnostics;

namespace TSOClient
{
    public class UILayer : IGraphicsLayer
    {
        private Microsoft.Xna.Framework.Game m_G;
        private List<UIScreen> m_Screens = new List<UIScreen>();
        private List<IUIProcess> m_UIProcess = new List<IUIProcess>();

        public UITooltipProperties TooltipProperties;
        public string Tooltip;

        private SpriteFont m_SprFontBig;
        private SpriteFont m_SprFontSmall;

        //For displaying 3D objects (sims).
        private Matrix m_WorldMatrix, m_ViewMatrix, m_ProjectionMatrix;
        private Dictionary<int, string> m_TextDict;

        //for fps counter
        private Stopwatch fpsStopwatch;

        /// <summary>
        /// Top most UI container
        /// </summary>
        private UIContainer mainUI;
        private UIContainer dialogContainer;

        private UIButton debugButton;
        public InputManager inputManager;
        private UIScreen currentScreen;

        /** Animation utility **/
        public UITween Tween;

        public Microsoft.Xna.Framework.Game GameComponent
        {
            get { return m_G; }
        }

        /// <summary>
        /// A worldmatrix, used to display 3D objects (sims).
        /// Initialized in the ScreenManager's constructor.
        /// </summary>
        public Matrix WorldMatrix
        {
            get { return m_WorldMatrix; }
            set { m_WorldMatrix = value; }
        }

        /// <summary>
        /// A viewmatrix, used to display 3D objects (sims).
        /// Initialized in the ScreenManager's constructor.
        /// </summary>
        public Matrix ViewMatrix
        {
            get { return m_ViewMatrix; }
            set { m_WorldMatrix = value; }
        }

        /// <summary>
        /// A projectionmatrix, used to display 3D objects (sims).
        /// Initialized in the ScreenManager's constructor.
        /// </summary>
        public Matrix ProjectionMatrix
        {
            get { return m_ProjectionMatrix; }
            set { m_ProjectionMatrix = value; }
        }

        /// <summary>
        /// The graphicsdevice that is part of the game instance.
        /// Used when calling XNA's graphic functions.
        /// </summary>
        public GraphicsDevice GraphicsDevice
        {
            get { return m_G.GraphicsDevice; }
        }

        /// <summary>
        /// A spritefont used to display big text.
        /// </summary>
        public SpriteFont SprFontBig
        {
            get { return m_SprFontBig; }
        }

        /// <summary>
        /// A spritefont used to display small text.
        /// </summary>
        public SpriteFont SprFontSmall
        {
            get { return m_SprFontSmall; }
        }

        /// <summary>
        /// The UIScreen instance that is currently being 
        /// updated and rendered by this ScreenManager instance.
        /// </summary>
        public UIScreen CurrentUIScreen
        {
            get
            {
                return currentScreen;
            }
        }

        /// <summary>
        /// Gets or sets the internal dictionary containing all the strings for the game.
        /// </summary>
        public Dictionary<int, string> TextDict
        {
            get { return m_TextDict; }
            set { m_TextDict = value; }
        }

        public UILayer(Microsoft.Xna.Framework.Game G, SpriteFont SprFontBig, SpriteFont SprFontSmall)
        {

            fpsStopwatch = new Stopwatch();
            fpsStopwatch.Start();

            m_G = G;
            m_SprFontBig = SprFontBig;
            m_SprFontSmall = SprFontSmall;

            m_WorldMatrix = Matrix.Identity;
            m_ViewMatrix = Matrix.CreateLookAt(Vector3.Right * 5, Vector3.Zero, Vector3.Forward);
            m_ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathHelper.Pi / 4.0f,
                    (float)GraphicsDevice.PresentationParameters.BackBufferWidth / 
                    (float)GraphicsDevice.PresentationParameters.BackBufferHeight,
                    1.0f, 100.0f);

            TextStyle.DefaultTitle = new TextStyle {
                Font = GameFacade.MainFont,
                Size = 10,
                Color = new Color(255,249,157),
                SelectedColor = new Color(0x00, 0x38, 0x7B),
                SelectionBoxColor = new Color(255, 249, 157)
            };

            TextStyle.DefaultButton = new TextStyle
            {
                Font = GameFacade.MainFont,
                Size = 10,
                Color = new Color(255, 249, 157),
                SelectedColor = new Color(0x00, 0x38, 0x7B),
                SelectionBoxColor = new Color(255, 249, 157)
            };

            TextStyle.DefaultLabel = new TextStyle
            {
                Font = GameFacade.MainFont,
                Size = 10,
                Color = new Color(255, 249, 157),
                SelectedColor = new Color(0x00, 0x38, 0x7B),
                SelectionBoxColor = new Color(255, 249, 157)
            };

            Tween = new UITween();
            this.AddProcess(Tween);

            inputManager = new InputManager();
            mainUI = new UIContainer();
            dialogContainer = new UIContainer();
            mainUI.Add(dialogContainer);

            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new UISpriteBatch(GraphicsDevice, 3);

            GameFacade.OnContentLoaderReady += new BasicEventHandler(GameFacade_OnContentLoaderReady);
            m_G.GraphicsDevice.DeviceReset += new EventHandler<EventArgs>(GraphicsDevice_DeviceReset);
        }

        private void GraphicsDevice_DeviceReset(object sender, EventArgs e)
        {
            for (int i = 0; i < m_Screens.Count; i++)
                m_Screens[i].DeviceReset(m_G.GraphicsDevice);
        }

        private void GameFacade_OnContentLoaderReady()
        {
            /**
             * Add a debug button once the content loader is ready so we can load textures
             */
            debugButton = new UIButton()
            {
                Caption = "Debug",
                Y = 10,
                Width = 100,
                X = GlobalSettings.Default.GraphicsWidth - 110
            };
            debugButton.OnButtonClick += new ButtonClickDelegate(debugButton_OnButtonClick);
            mainUI.Add(debugButton);
        }

        void debugButton_OnButtonClick(UIElement button)
        {
            GameFacade.Controller.StartDebugTools();
        }

        public void AddProcess(IUIProcess Proc)
        {
            m_UIProcess.Add(Proc);
        }

        public void RemoveProcess(IUIProcess Proc)
        {
            m_UIProcess.Remove(Proc);
        }

        /// <summary>
        /// Adds a UIScreen instance to this ScreenManager's list of screens.
        /// This function is called from Lua.
        /// </summary>
        /// <param name="Screen">The UIScreen instance to be added.</param>
        public void AddScreen(UIScreen Screen)
        {
            /*if (currentScreen != null)
            {
                mainUI.Remove(currentScreen);
            }*/
            /** Add screen on top **/
            mainUI.Add(Screen);
            /** Bring dialogs to top **/
            mainUI.Add(dialogContainer);
            /** Bring debug to the top **/
            mainUI.Add(debugButton);

            Screen.OnShow();

            m_Screens.Add(Screen);
            currentScreen = Screen;
        }

        public void RemoveScreen(UIScreen Screen)
        {
            if (Screen == currentScreen)
            {
                currentScreen = null;
            }
            Screen.OnHide();
            mainUI.Remove(Screen);
            m_Screens.Remove(Screen);

            /** Put the previous screen back into the UI **/
            if (m_Screens.Count > 0)
            {
                currentScreen = m_Screens.Last();
                mainUI.AddAt(0, currentScreen);
            }
        }

        public void RemoveCurrent()
        {
            /** Remove all dialogs **/
            while (Dialogs.Count > 0)
            {
                RemoveDialog(Dialogs[0]);
            }

            var currentScreen = mainUI.GetChildren().OfType<UIScreen>().FirstOrDefault();
            if (currentScreen != null)
            {
                ((UIScreen)currentScreen).OnHide();
                mainUI.Remove(currentScreen);
            }
        }

        private float m_FPS = 0.0f;

        public void Update(UpdateState state)
        {

            /** 
             * Handle the mouse events from the previous frame
             * Its important to do this before the update calls because
             * a lot of mouse events will make changes to the UI. If they do
             * we want the Matrix's to be recalculated before the draw
             * method and that is done in the update method.
             */

            inputManager.HandleMouseEvents(state);
            state.MouseEvents.Clear();

            state.InputManager = inputManager;
            mainUI.Update(state);

            /** Process external update handlers **/
            foreach (var item in m_UIProcess)
            {
                item.Update(state);
            }
        }

        public void PreDraw(UISpriteBatch SBatch)
        {
            mainUI.PreDraw(SBatch);
        }

        public void Draw(UISpriteBatch SBatch, float FPS)
        {
            fpsStopwatch.Stop();
            m_FPS = (float)(1000.0f / fpsStopwatch.ElapsedMilliseconds);
            fpsStopwatch.Reset();
            fpsStopwatch.Start();

            mainUI.Draw(SBatch);

            if (TooltipProperties.UpdateDead) TooltipProperties.Show = false;
            if (Tooltip != null && TooltipProperties.Show) DrawTooltip(SBatch, TooltipProperties.Position, TooltipProperties.Opacity);
            TooltipProperties.UpdateDead = true;

            SBatch.DrawString(m_SprFontBig, "FPS: " + FPS.ToString(), new Vector2(0, 0), Color.Red);
        }

        public void DrawTooltip(SpriteBatch batch, Vector2 position, float opacity)
        {
            TextStyle style = TextStyle.DefaultLabel.Clone();
            style.Color = Color.Black;
            style.Size = 8;

            var scale = new Vector2(1, 1);
            if (style.Scale != 1.0f)
            {
                scale = new Vector2(scale.X * style.Scale, scale.Y * style.Scale);
            }

            var wrapped = UIUtils.WordWrap(Tooltip, 290, style, scale); //tooltip max width should be 300. There is a 5px margin on each side.

            int width = wrapped.MaxWidth + 10;
            int height = 13 * wrapped.Lines.Count + 4; //13 per line + 4.

            position.X = Math.Min(position.X, GlobalSettings.Default.GraphicsWidth - width);
            position.Y = Math.Max(position.Y, height);

            var whiteRectangle = new Texture2D(batch.GraphicsDevice, 1, 1);
            whiteRectangle.SetData(new[] { Color.White });

            batch.Draw(whiteRectangle, new Rectangle((int)position.X, (int)position.Y - height, width, height), Color.White*opacity); //note: in XNA4 colours need to be premultiplied

            //border
            batch.Draw(whiteRectangle, new Rectangle((int)position.X, (int)position.Y - height, 1, height), new Color(0, 0, 0, opacity));
            batch.Draw(whiteRectangle, new Rectangle((int)position.X, (int)position.Y - height, width, 1), new Color(0, 0, 0, opacity));
            batch.Draw(whiteRectangle, new Rectangle((int)position.X + width, (int)position.Y - height, 1, height), new Color(0, 0, 0, opacity));
            batch.Draw(whiteRectangle, new Rectangle((int)position.X, (int)position.Y, width, 1), new Color(0, 0, 0, opacity));

            position.Y -= height;

            for (int i = 0; i < wrapped.Lines.Count; i++)
            {
                int thisWidth = (int)(style.SpriteFont.MeasureString(wrapped.Lines[i]).X * scale.X);
                batch.DrawString(style.SpriteFont, wrapped.Lines[i], position + new Vector2((width - thisWidth) / 2, 0), new Color(0, 0, 0, opacity), 0, Vector2.Zero, scale, SpriteEffects.None, 0);
                position.Y += 13;
            }
        }

        private List<DialogReference> Dialogs = new List<DialogReference>();
        public void AddDialog(DialogReference dialog)
        {
            //dialogContainer.Add(dialog.Dialog);
            CurrentUIScreen.Add(dialog.Dialog);
            Dialogs.Add(dialog);
            AdjustModal();
        }

        public void RemoveDialog(DialogReference dialog)
        {
            //dialogContainer.Remove(dialog.Dialog);
            if (dialog.Dialog.Parent != null)
            {
                dialog.Dialog.Parent.Remove(dialog.Dialog);
            }
            Dialogs.Remove(dialog);
            AdjustModal();
        }

        public void RemoveDialog(UIElement dialog)
        {
            var reference = Dialogs.FirstOrDefault(x => x.Dialog == dialog);
            if (reference != null)
            {
                Dialogs.Remove(reference);
                dialog.Parent.Remove(reference.Dialog);
                AdjustModal();
            }
        }

        private UIBlocker ModalBlocker = new UIBlocker();
        private void AdjustModal()
        {
            var topMostModal = Dialogs.LastOrDefault(x => x.Modal);
            /** Remove modal blocker **/
            if (ModalBlocker.Parent != null)
            {
                ModalBlocker.Parent.Remove(ModalBlocker);
            }

            if (topMostModal == null)
            {
                
            }
            else
            {
                CurrentUIScreen.AddBefore(ModalBlocker, topMostModal.Dialog);
            }
        }


        #region IGraphicsLayer Members

        UISpriteBatch spriteBatch;

        public void PreDraw(GraphicsDevice device)
        {
            spriteBatch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
            this.PreDraw(spriteBatch);
            spriteBatch.End();
        }

        public void Draw(GraphicsDevice device)
        {
            spriteBatch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
            this.Draw(spriteBatch, m_FPS);
            spriteBatch.End();
        }

        #endregion

        #region IGraphicsLayer Members

        public void Initialize(GraphicsDevice device)
        {
        }

        #endregion
    }

    public delegate void UpdateHookDelegate(UpdateState state);

    public class DialogReference
    {
        public UIElement Dialog;
        public bool Modal;
    }
}
