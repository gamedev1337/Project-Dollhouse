﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using tso.world;
using TSO.Simantics;
using TSO.Common.rendering.framework.model;
using Microsoft.Xna.Framework;
using tso.world.components;
using TSO.Simantics.entities;
using tso.world.model;
using TSOClient.Code.UI.Model;
using TSO.HIT;
using TSO.Simantics.model;
using Microsoft.Xna.Framework.Input;
using TSOClient.Code.UI.Framework;

namespace TSOClient.Code.UI.Panels
{
    public class UIObjectHolder //controls the object holder interface
    {
        public VM vm;
        public World World;
        public UILotControl ParentControl;

        public Direction Rotation;
        public int MouseDownX;
        public int MouseDownY;
        private bool MouseIsDown;
        private bool MouseWasDown;
        private bool MouseClicked;
        public bool DirChanged;
        public bool ShowTooltip;

        public event HolderEventHandler OnPickup;
        public event HolderEventHandler OnDelete;
        public event HolderEventHandler OnPutDown;

        public UIObjectSelection Holding;

        public UIObjectHolder(VM vm, World World, UILotControl parent)
        {
            this.vm = vm;
            this.World = World;
            ParentControl = parent;
        }

        public void SetSelected(VMMultitileGroup Group)
        {
            if (Holding != null) ClearSelected();
            Holding = new UIObjectSelection();
            Holding.Group = Group;
            Holding.PreviousTile = Holding.Group.BaseObject.Position;
            Holding.Dir = Group.Objects[0].Direction;
            VMEntity[] CursorTiles = new VMEntity[Group.Objects.Count];
            for (int i = 0; i < Group.Objects.Count; i++)
            {
                var target = Group.Objects[i];
                if (target is VMGameObject) ((ObjectComponent)target.WorldUI).ForceDynamic = true;
                CursorTiles[i] = vm.Context.CreateObjectInstance(0x00000437, new LotTilePos(target.Position), tso.world.model.Direction.NORTH).Objects[0];
                CursorTiles[i].SetPosition(new LotTilePos(0,0,1), Direction.NORTH, vm.Context);
                ((ObjectComponent)CursorTiles[i].WorldUI).ForceDynamic = true;
            }
            Holding.TilePosOffset = new Vector2(0, 0);
            Holding.CursorTiles = CursorTiles;
        }

        public void MoveSelected(Vector2 pos, sbyte level)
        {
            Holding.TilePos = pos;
            Holding.Level = level;

            //first, eject the object from any slots
            for (int i = 0; i < Holding.Group.Objects.Count; i++)
            {
                var obj = Holding.Group.Objects[i];
                if (obj.Container != null)
                {
                    obj.Container.ClearSlot(obj.ContainerSlot);
                }
            }

            //rotate through to try all configurations
            var dir = Holding.Dir;
            VMPlacementError status = VMPlacementError.Success;
            for (int i = 0; i < 4; i++)
            {
                status = Holding.Group.ChangePosition(LotTilePos.FromBigTile((short)pos.X, (short)pos.Y, World.State.Level), dir, vm.Context);
                if (status != VMPlacementError.MustBeAgainstWall) break;
                dir = (Direction)((((int)dir << 6) & 255) | ((int)dir >> 2));
            }
            if (Holding.Dir != dir) Holding.Dir = dir;

            if (status != VMPlacementError.Success) 
            {
                Holding.Group.ChangePosition(LotTilePos.OUT_OF_WORLD, Holding.Dir, vm.Context);

                Holding.Group.SetVisualPosition(new Vector3(pos,
                (((Holding.Group.Objects[0].GetValue(VMStackObjectVariable.AllowedHeightFlags) & 1) == 1) ? 0 : 4f / 5f) + (World.State.Level-1)*2.95f),
                    //^ if we can't be placed on the floor, default to table height.
                Holding.Dir, vm.Context);
            }

            for (int i = 0; i < Holding.Group.Objects.Count; i++)
            {
                var target = Holding.Group.Objects[i];
                var tpos = target.VisualPosition;
                tpos.Z = (World.State.Level - 1)*2.95f;
                Holding.CursorTiles[i].MultitileGroup.SetVisualPosition(tpos, Holding.Dir, vm.Context);
            }
            Holding.CanPlace = status;
        }

        public void ClearSelected()
        {
            //TODO: selected items are only spooky ghosts of the items themselves.
            //      ...so that they dont cause serverside desyncs
            //      and so that clearing selections doesnt delete already placed objects.
            if (Holding != null)
            {
                for (int i = 0; i < Holding.Group.Objects.Count; i++)
                {
                    var target = Holding.Group.Objects[i];
                    if (target is VMGameObject) ((ObjectComponent)target.WorldUI).ForceDynamic = false;
                }

                for (int i = 0; i < Holding.CursorTiles.Length; i++) {
                    Holding.CursorTiles[i].Delete(true, vm.Context);
                    ((ObjectComponent)Holding.CursorTiles[i].WorldUI).ForceDynamic = false;
                }
            }
            Holding = null;
        }

        public void MouseDown(UpdateState state)
        {
            MouseIsDown = true;
            MouseDownX = state.MouseState.X;
            MouseDownY = state.MouseState.Y;
            if (Holding != null)
            {
                Rotation = Holding.Dir;
                DirChanged = false;
            }
        }

        public void MouseUp(UpdateState state)
        {
            MouseIsDown = false;
            if (Holding != null && Holding.Clicked)
            {
                if (Holding.CanPlace == VMPlacementError.Success)
                {
                    HITVM.Get().PlaySoundEvent((Holding.IsBought) ? UISounds.ObjectMovePlace : UISounds.ObjectPlace);
                    ExecuteEntryPoint(11); //User Placement
                    var putDown = Holding;
                    ClearSelected();
                    if (OnPutDown != null) OnPutDown(putDown, state); //call this after so that buy mode etc can produce more.
                }
                else
                {
                    
                }
            }

            GameFacade.Screens.TooltipProperties.Show = false;
            GameFacade.Screens.TooltipProperties.Opacity = 0;
            ShowTooltip = false;
        }

        private void ExecuteEntryPoint(int num)
        {
            for (int i = 0; i < Holding.Group.Objects.Count; i++) Holding.Group.Objects[i].ExecuteEntryPoint(num, vm.Context, true); 
        }

        public void SellBack(UIElement button)
        {
            if (Holding == null) return;
            if (Holding.IsBought) HITVM.Get().PlaySoundEvent(UISounds.MoneyBack);
            Holding.Group.Delete(vm.Context);
            OnDelete(Holding, null); //TODO: cleanup callbacks which don't need updatestate into another delegate. will do when refactoring for online
            ClearSelected();
        }

        public void Update(UpdateState state, bool scrolled)
        {
            if (ShowTooltip) GameFacade.Screens.TooltipProperties.UpdateDead = false;
            MouseClicked = (MouseIsDown && (!MouseWasDown));

            if (state.KeyboardState.IsKeyDown(Keys.Delete) && Holding != null)
            {
                SellBack(null);
            }
            if (Holding != null)
            {
                if (MouseClicked) Holding.Clicked = true;
                //TODO: crash if placed out of world
                if (MouseIsDown && Holding.Clicked)
                {
                    bool updatePos = MouseClicked;
                    int xDiff = state.MouseState.X - MouseDownX;
                    int yDiff = state.MouseState.Y - MouseDownY;
                    if (Math.Sqrt(xDiff * xDiff + yDiff * yDiff) > 64)
                    {
                        int dir;
                        if (xDiff > 0)
                        {
                            if (yDiff > 0) dir = 1;
                            else dir = 0;
                        }
                        else
                        {
                            if (yDiff > 0) dir = 2;
                            else dir = 3;
                        }
                        var newDir = (Direction)(1 << (((dir + 4 - (int)World.State.Rotation) % 4) * 2));
                        if (newDir != Holding.Dir || MouseClicked)
                        {
                            updatePos = true;
                            HITVM.Get().PlaySoundEvent(UISounds.ObjectRotate);
                            Holding.Dir = newDir;
                            DirChanged = true;
                        }
                    }
                    if (updatePos)
                    {
                        MoveSelected(Holding.TilePos, Holding.Level);
                        if (Holding.CanPlace != VMPlacementError.Success)
                        {
                            GameFacade.Screens.TooltipProperties.Show = true;
                            GameFacade.Screens.TooltipProperties.Opacity = 1;
                            GameFacade.Screens.TooltipProperties.Position = new Vector2(MouseDownX,
                                MouseDownY);
                            GameFacade.Screens.Tooltip = GameFacade.Strings.GetString("137", "kPErr" + Holding.CanPlace.ToString()
                                + ((Holding.CanPlace == VMPlacementError.CannotPlaceComputerOnEndTable) ? "," : ""));
                            // comma added to curcumvent problem with language file. We should probably just index these with numbers?
                            GameFacade.Screens.TooltipProperties.UpdateDead = false;
                            ShowTooltip = true;
                            HITVM.Get().PlaySoundEvent(UISounds.Error);
                        }
                        else
                        {
                            GameFacade.Screens.TooltipProperties.Show = false;
                            GameFacade.Screens.TooltipProperties.Opacity = 0;
                            ShowTooltip = false;
                        }
                    }
                }
                else
                {
                    var tilePos = World.State.WorldSpace.GetTileAtPosWithScroll(new Vector2(state.MouseState.X, state.MouseState.Y))+Holding.TilePosOffset;
                    MoveSelected(tilePos, 1);
                }
            }
            else
            {
                //not holding an object, but one can be selected
                var newHover = World.GetObjectIDAtScreenPos(state.MouseState.X, state.MouseState.Y, GameFacade.GraphicsDevice);
                if (MouseClicked && (newHover != 0))
                {
                    var objGroup = vm.GetObjectById(newHover).MultitileGroup;
                    var objBasePos = objGroup.BaseObject.Position;
                    if (objBasePos.Level == World.State.Level)
                    {
                        SetSelected(objGroup);

                        Holding.TilePosOffset = new Vector2(objBasePos.x / 16f, objBasePos.y / 16f) - World.State.WorldSpace.GetTileAtPosWithScroll(new Vector2(state.MouseState.X, state.MouseState.Y));
                        if (OnPickup != null) OnPickup(Holding, state);
                        ExecuteEntryPoint(12); //User Pickup
                    }
                    else
                    {
                        GameFacade.Screens.TooltipProperties.Show = true;
                        GameFacade.Screens.TooltipProperties.Opacity = 1;
                        GameFacade.Screens.TooltipProperties.Position = new Vector2(MouseDownX,
                            MouseDownY);
                        GameFacade.Screens.Tooltip = GameFacade.Strings.GetString("137", "kPErr" + VMPlacementError.CantEffectFirstLevelFromSecondLevel.ToString());
                        GameFacade.Screens.TooltipProperties.UpdateDead = false;
                        ShowTooltip = true;
                        HITVM.Get().PlaySoundEvent(UISounds.Error);
                    }
                }
            }

            MouseWasDown = MouseIsDown;
        }

        public delegate void HolderEventHandler(UIObjectSelection holding, UpdateState state);
    }

    public class UIObjectSelection
    {
        public VMMultitileGroup Group;
        public VMEntity[] CursorTiles;
        public LotTilePos PreviousTile;
        public Direction Dir = Direction.NORTH;
        public Vector2 TilePos;
        public Vector2 TilePosOffset;
        public bool Clicked;
        public VMPlacementError CanPlace;
        public sbyte Level;

        public bool IsBought
        {
            get
            {
                return PreviousTile != LotTilePos.OUT_OF_WORLD;
            }
        }
    }
}
