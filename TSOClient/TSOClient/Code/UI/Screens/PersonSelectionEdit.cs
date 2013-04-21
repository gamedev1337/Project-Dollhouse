﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TSOClient.Code.UI.Framework;
using Microsoft.Xna.Framework.Graphics;
using TSOClient.Code.UI.Controls;
using TSOClient.Code.Data.Model;
using TSOClient.LUI;
using TSOServiceClient.Model;
using TSOClient.Code.UI.Framework.Parser;

namespace TSOClient.Code.UI.Screens
{
    public class PersonSelectionEdit : GameScreen
    {
        /** UI created by script **/
        public Texture2D BackgroundImage { get; set; }
        public Texture2D BackgroundImageDialog { get; set; }
        public UIButton CancelButton { get; set; }
        public UIButton AcceptButton { get; set; }
        public UIButton SkinLightButton { get; set; }
        public UIButton SkinMediumButton { get; set; }
        public UIButton SkinDarkButton { get; set; }
        public UIButton FemaleButton { get; set; }
        public UIButton MaleButton { get; set; }

        public UITextEdit NameTextEdit { get; set; }
        public UITextEdit DescriptionTextEdit { get; set; }
        public UIButton DescriptionScrollUpButton { get; set; }
        public UIButton DescriptionScrollDownButton { get; set; }
        public UISlider DescriptionSlider { get; set; }

        private UICollectionViewer HeadSkinBrowser;
        private UICollectionViewer BodySkinBrowser;
        
        /** Data **/
        private Collection MaleHeads;
        private Collection MaleOutfits;
        private Collection FemaleHeads;
        private Collection FemaleOutfits;

        /** State **/
        private AppearanceType AppearanceType = AppearanceType.Light;
        private UIButton SelectedAppearanceButton;
        private Gender Gender = Gender.Female;

        public CityInfo SelectedCity;


        public PersonSelectionEdit()
        {
            /**
             * Data
             */
            MaleHeads = new Collection(ContentManager.GetResourceFromLongID((ulong)FileIDs.CollectionsFileIDs.ea_male_heads));
            MaleOutfits = new Collection(ContentManager.GetResourceFromLongID((ulong)FileIDs.CollectionsFileIDs.ea_male));

            FemaleHeads = new Collection(ContentManager.GetResourceFromLongID((ulong)FileIDs.CollectionsFileIDs.ea_female_heads));
            FemaleOutfits = new Collection(ContentManager.GetResourceFromLongID((ulong)FileIDs.CollectionsFileIDs.ea_female));

            /**
             * UI
             */

            UIScript ui = null;
            if (GlobalSettings.Default.ScaleUI)
            {
                ui = this.RenderScript("personselectionedit.uis");
                this.Scale800x600 = true;
            }
            else
            {
                ui = this.RenderScript("personselectionedit" + (ScreenWidth == 1024 ? "1024" : "") + ".uis");
            }

            DescriptionTextEdit.CurrentText = ui.GetString("DefaultAvatarDescription");
            DescriptionSlider.AttachButtons(DescriptionScrollUpButton, DescriptionScrollDownButton, 1);
            DescriptionTextEdit.AttachSlider(DescriptionSlider);
            NameTextEdit.OnChange += new ChangeDelegate(NameTextEdit_OnChange);


            AcceptButton.Disabled = true;

            /** Appearance **/
            SkinLightButton.OnButtonClick += new ButtonClickDelegate(SkinButton_OnButtonClick);
            SkinMediumButton.OnButtonClick += new ButtonClickDelegate(SkinButton_OnButtonClick);
            SkinDarkButton.OnButtonClick += new ButtonClickDelegate(SkinButton_OnButtonClick);
            SelectedAppearanceButton = SkinLightButton;
            SkinLightButton.Selected = true;
            
            HeadSkinBrowser = ui.Create<UICollectionViewer>("HeadSkinBrowser");
            HeadSkinBrowser.OnChange += new ChangeDelegate(HeadSkinBrowser_OnChange);
            HeadSkinBrowser.Init();
            this.Add(HeadSkinBrowser);

            BodySkinBrowser = ui.Create<UICollectionViewer>("BodySkinBrowser");
            BodySkinBrowser.Init();
            this.Add(BodySkinBrowser);

            FemaleButton.OnButtonClick += new ButtonClickDelegate(GenderButton_OnButtonClick);
            MaleButton.OnButtonClick += new ButtonClickDelegate(GenderButton_OnButtonClick);
            



            /** Backgrounds **/
            this.AddAt(0, new UIImage(BackgroundImage));
            if (BackgroundImageDialog != null)
            {
                this.AddAt(1, new UIImage(BackgroundImageDialog) {
                    X = 112,
                    Y = 84
                });
            }

            /**
             * Music
             */
            PlayBackgroundMusic(
                GameFacade.GameFilePath("music\\modes\\create\\tsocas1_v2.mp3")
            );


            /**
             * Init state
             */
            RefreshCollections();
            FemaleButton.Selected = true;

        }

        void HeadSkinBrowser_OnChange(UIElement element)
        {
            //System.Diagnostics.Debug.WriteLine(((CollectionItem)HeadSkinBrowser.SelectedItem).PurchasableObject.OutfitID);
        }

        void NameTextEdit_OnChange(UIElement element)
        {
            AcceptButton.Disabled = NameTextEdit.CurrentText.Length == 0;
        }

        void GenderButton_OnButtonClick(UIElement button)
        {
            if (button == MaleButton)
            {
                Gender = Gender.Male;
                MaleButton.Selected = true;
                FemaleButton.Selected = false;
            }
            else if (button == FemaleButton)
            {
                Gender = Gender.Female;
                MaleButton.Selected = false;
                FemaleButton.Selected = true;
            }
            RefreshCollections();
        }


        void RefreshCollections()
        {
            if (Gender == Gender.Male)
            {
                HeadSkinBrowser.DataProvider = CollectionToDataProvider(MaleHeads);
                BodySkinBrowser.DataProvider = CollectionToDataProvider(MaleOutfits);
            }
            else
            {
                HeadSkinBrowser.DataProvider = CollectionToDataProvider(FemaleHeads);
                BodySkinBrowser.DataProvider = CollectionToDataProvider(FemaleOutfits);
            }
        }

        
        private List<object> CollectionToDataProvider(Collection collection)
        {
            var dataProvider = new List<object>();
            foreach (var outfit in collection)
            {
                var thumbID = outfit.PurchasableObject.Outfit.GetAppearance(AppearanceType).ThumbnailID;

                dataProvider.Add(new UIGridViewerItem {
                    Data = outfit,
                    Thumb = new TSOClient.Code.Utils.Promise<Texture2D>(x => GetTexture(thumbID))
                });
            }
            return dataProvider;
        }

        void SkinButton_OnButtonClick(UIElement button)
        {
            SelectedAppearanceButton.Selected = false;
            SelectedAppearanceButton = (UIButton)button;
            SelectedAppearanceButton.Selected = true;


            var type = AppearanceType.Light;

            if (button == SkinMediumButton)
            {
                type = AppearanceType.Medium;
            }
            else if (button == SkinDarkButton)
            {
                type = AppearanceType.Dark;
            }

            this.AppearanceType = type;
            RefreshCollections();
        }

        


    }

    public enum Gender
    {
        Male,
        Female
    }
}