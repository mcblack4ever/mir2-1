﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using Client.MirSounds;
using Client.MirControls;
using S = ServerPackets;
using C = ClientPackets;

namespace Client.MirObjects
{
    public class PlayerObject : MapObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Player; }
        }

        public override bool Blocking
        {
            get { return !Dead; }
        }

        public MirGender Gender;
        public MirClass Class;
        public byte Hair;
        public ushort Level;

        public MLibrary WeaponLibrary1, WeaponLibrary2, HairLibrary, WingLibrary, MountLibrary;
        public int Armour, Weapon, ArmourOffSet, HairOffSet, WeaponOffSet, WingOffset, MountOffset;

        public int DieSound, FlinchSound, AttackSound;


        public FrameSet Frames;
        public Frame Frame, WingFrame;
        public int FrameIndex, FrameInterval, EffectFrameIndex, EffectFrameInterval, SlowFrameIndex;
        public byte SkipFrameUpdate = 0;

        public bool HasClassWeapon
        {
            get
            {
                switch (Weapon / 100)
                {
                    default:
                        return Class == MirClass.Wizard || Class == MirClass.Warrior || Class == MirClass.Taoist;
                    case 1:
                        return Class == MirClass.Assassin;
                    case 2:
                        return Class == MirClass.Archer;
                }
            }
        }

        public bool HasFishingRod
        {
            get
            {
                return Weapon == 49 || Weapon == 50;
            }
        }

        public Spell Spell;
        public byte SpellLevel;
        public int JumpDistance;
        public bool Cast;
        public uint TargetID;
        public Point TargetPoint;

        public bool MagicShield;
        public Effect ShieldEffect;

        public bool ElementalBarrier;
        public Effect ElementalBarrierEffect;

        public byte WingEffect;
        private short StanceDelay = 2500;

        //ArcherSpells - Elemental system
        public bool ElementalBuff;
        public bool Concentrating;
        public InterruptionEffect ConcentratingEffect;
        public bool ConcentrateInterrupted;
        public bool HasElements;
        public bool ElementCasted;
        public int ElementEffect;//hold orb count for player(object) load
        public int ElementsLevel;
        public int ElementOrbMax;
        //Elemental system END

        public SpellEffect CurrentEffect;

        public bool RidingMount, Sprint, FastRun, Fishing, FoundFish;
        public long StanceTime, MountTime, FishingTime;
        public long BlizzardStopTime, ReincarnationStopTime, SlashingBurstTime;

        public short MountType = -1, TransformType = -1;

        public string GuildName;
        public string GuildRankName;

        public Point FishingPoint;

        public LevelEffects LevelEffects;

        public PlayerObject(uint objectID)
            : base(objectID)
        {
            Frames = FrameSet.Players;
        }

        public void Load(S.ObjectPlayer info)
        {
            Name = info.Name;
            NameColour = info.NameColour;
            GuildName = info.GuildName;
            GuildRankName = info.GuildRankName;
            Class = info.Class;
            Gender = info.Gender;
            Level = info.Level;

            CurrentLocation = info.Location;
            MapLocation = info.Location;
            GameScene.Scene.MapControl.AddObject(this);

            Direction = info.Direction;
            Hair = info.Hair;

            Weapon = info.Weapon;
            Armour = info.Armour;
            Light = info.Light;

            Poison = info.Poison;

            Dead = info.Dead;
            Hidden = info.Hidden;

            WingEffect = info.WingEffect;
            CurrentEffect = info.Effect;

            MountType = info.MountType;
            RidingMount = info.RidingMount;

            Fishing = info.Fishing;

            TransformType = info.TransformType;

            SetLibraries();

            if (Dead) ActionFeed.Add(new QueuedAction { Action = MirAction.Dead, Direction = Direction, Location = CurrentLocation });
            if (info.Extra) Effects.Add(new Effect(Libraries.Magic2, 670, 10, 800, this));

            ElementEffect = (int)info.ElementOrbEffect;
            ElementsLevel = (int)info.ElementOrbLvl;
            ElementOrbMax = (int)info.ElementOrbMax;

            Buffs = info.Buffs;

            LevelEffects = info.LevelEffects;

            ProcessBuffs();

            SetAction();

            SetEffects();
        }
        public void Update(S.PlayerUpdate info)
        {
            Weapon = info.Weapon;
            Armour = info.Armour;
            Light = info.Light;
            WingEffect = info.WingEffect;

            SetLibraries();
            SetEffects();
        }

        public void ProcessBuffs()
        {
            for (int i = 0; i < Buffs.Count; i++)
            {
                AddBuffEffect(Buffs[i]);
            }
        }

        public void MountUpdate(S.MountUpdate info)
        {
            MountType = info.MountType;
            RidingMount = info.RidingMount;

            QueuedAction action = new QueuedAction { Action = MirAction.Standing, Direction = Direction, Location = CurrentLocation };
            ActionFeed.Insert(0, action);

            MountTime = CMain.Time;

            if (MountType < 0)
                GameScene.Scene.MountDialog.Hide();

            SetLibraries();
            SetEffects();

            PlayMountSound();
        }

        public void FishingUpdate(S.FishingUpdate p)
        {
            if (Fishing != p.Fishing)
            {
                MirDirection dir = Functions.DirectionFromPoint(CurrentLocation, p.FishingPoint);

                if (p.Fishing)
                {        
                    QueuedAction action = new QueuedAction { Action = MirAction.FishingCast, Direction = dir, Location = CurrentLocation };
                    ActionFeed.Add(action);
                }
                else
                {
                    QueuedAction action = new QueuedAction { Action = MirAction.FishingReel, Direction = dir, Location = CurrentLocation };
                    ActionFeed.Add(action);

                    if (p.FoundFish)
                        GameScene.Scene.ChatDialog.ReceiveChat("Found fish!!", ChatType.Hint);
                }

                Fishing = p.Fishing;
                SetLibraries();
            }

            if (!HasFishingRod)
            {
                GameScene.Scene.FishingDialog.Hide();
            }          

            FishingPoint = p.FishingPoint;
            FoundFish = p.FoundFish;
        }


        public virtual void SetLibraries()
        {
            //fishing broken
            //10
            //11
            //12
            //13

            //almost all broken
            //20 - black footballer - 791
            //21 - red footballer - 791
            //22 - blue footballer - 791
            //23 - green footballer - 791
            //24 - red2 footballer - 791

            bool altAnim = false;

            bool showMount = true;
            bool showFishing = true;

            if (TransformType > -1)
            {
                #region Transform
                
                switch (TransformType)
                {
                    case 4:
                    case 5:
                    case 7:
                    case 8:                
                    case 26:
                        showFishing = false;
                        break;
                    case 6:
                    case 9:
                        showMount = false;
                        showFishing = false;
                        break;
                    default:
                        break;
                }

                switch (CurrentAction)
                {
                    case MirAction.Standing:
                    case MirAction.Jump:
                        Frames.Frames.TryGetValue(MirAction.Standing, out Frame);
                        break;
                    case MirAction.Walking:
                    case MirAction.WalkingBow:
                        Frames.Frames.TryGetValue(MirAction.Walking, out Frame);
                        break;
                    case MirAction.Running:
                    case MirAction.RunningBow:
                        Frames.Frames.TryGetValue(MirAction.Running, out Frame);
                        break;
                    case MirAction.Attack1:
                    case MirAction.Attack2:
                    case MirAction.Attack3:
                    case MirAction.Attack4:
                    case MirAction.AttackRange1:
                    case MirAction.AttackRange2:
                    case MirAction.AttackRange3:
                        Frames.Frames.TryGetValue(MirAction.Attack1, out Frame);
                        break;
                }

                if (MountType > 6 && RidingMount)
                {
                    ArmourOffSet = -416;
                    BodyLibrary = TransformType < Libraries.TransformMounts.Length ? Libraries.TransformMounts[TransformType] : Libraries.TransformMounts[0];
                }
                else
                {
                    ArmourOffSet = 0;
                    BodyLibrary = TransformType < Libraries.Transform.Length ? Libraries.Transform[TransformType] : Libraries.Transform[0];
                }

                HairLibrary = null;
                WeaponLibrary1 = null;
                WeaponLibrary2 = null;

                if (TransformType == 19)
                {
                    WingEffect = 2;
                    WingLibrary = WingEffect - 1 < Libraries.TransformEffect.Length ? Libraries.TransformEffect[WingEffect - 1] : null;
                }
                else
                {
                    WingLibrary = null;
                }

                HairOffSet = 0;
                WeaponOffSet = 0;
                WingOffset = 0;
                MountOffset = 0;

                #endregion
            }
            else
            {

                switch (Class)
                {
                    #region Archer
                    case MirClass.Archer:

                        #region WeaponType
                        if (HasClassWeapon)
                        {
                            switch (CurrentAction)
                            {
                                case MirAction.Walking:
                                case MirAction.Running:
                                case MirAction.AttackRange1:
                                case MirAction.AttackRange2:
                                    altAnim = true;
                                    break;
                            }
                        }

                        if (CurrentAction == MirAction.Jump) altAnim = true;

                        #endregion

                        #region Armours
                        if (altAnim)
                        {
                            switch (Armour)
                            {
                                case 9: //heaven
                                case 10: //mir
                                case 11: //oma
                                case 12: //spirit
                                    BodyLibrary = Armour + 1 < Libraries.ARArmours.Length ? Libraries.ARArmours[Armour + 1] : Libraries.ARArmours[0];
                                    break;

                                case 19:
                                    BodyLibrary = Armour - 5 < Libraries.ARArmours.Length ? Libraries.ARArmours[Armour - 5] : Libraries.ARArmours[0];
                                    break;

                                case 29:
                                case 30:
                                    BodyLibrary = Armour - 14 < Libraries.ARArmours.Length ? Libraries.ARArmours[Armour - 14] : Libraries.ARArmours[0];
                                    break;

                                case 35:
                                case 36:
                                case 37:
                                case 38:
                                case 39:
                                case 40:
                                case 41:
                                    BodyLibrary = Armour - 32 < Libraries.ARArmours.Length ? Libraries.ARArmours[Armour - 32] : Libraries.ARArmours[0];
                                    break;

                                default:
                                    BodyLibrary = Armour < Libraries.ARArmours.Length ? Libraries.ARArmours[Armour] : Libraries.ARArmours[0];
                                    break;
                            }

                            HairLibrary = Hair < Libraries.ARHair.Length ? Libraries.ARHair[Hair] : null;
                        }
                        else
                        {
                            BodyLibrary = Armour < Libraries.CArmours.Length ? Libraries.CArmours[Armour] : Libraries.CArmours[0];
                            HairLibrary = Hair < Libraries.CHair.Length ? Libraries.CHair[Hair] : null;
                        }
                        #endregion

                        #region Weapons
                        if (HasClassWeapon)
                        {
                            int Index = Weapon - 200;

                            if (altAnim)
                                WeaponLibrary2 = Index < Libraries.ARWeaponsS.Length ? Libraries.ARWeaponsS[Index] : null;
                            else
                                WeaponLibrary2 = Index < Libraries.ARWeapons.Length ? Libraries.ARWeapons[Index] : null;

                            WeaponLibrary1 = null;
                        }
                        else
                        {
                            if (Weapon >= 0)
                                WeaponLibrary1 = Weapon < Libraries.CWeapons.Length ? Libraries.CWeapons[Weapon] : null;
                            else
                                WeaponLibrary1 = null;

                            WeaponLibrary2 = null;
                        }
                        #endregion

                        #region WingEffects
                        if (WingEffect > 0 && WingEffect < 100)
                        {
                            if (altAnim)
                                WingLibrary = (WingEffect - 1) < Libraries.ARHumEffect.Length ? Libraries.ARHumEffect[WingEffect - 1] : null;
                            else
                                WingLibrary = (WingEffect - 1) < Libraries.CHumEffect.Length ? Libraries.CHumEffect[WingEffect - 1] : null;
                        }
                        #endregion

                        #region Offsets
                        ArmourOffSet = Gender == MirGender.Male ? 0 : altAnim ? 352 : 808;
                        HairOffSet = Gender == MirGender.Male ? 0 : altAnim ? 352 : 808;
                        WeaponOffSet = Gender == MirGender.Male ? 0 : altAnim ? 352 : 416;
                        WingOffset = Gender == MirGender.Male ? 0 : altAnim ? 352 : 840;
                        MountOffset = 0;
                        #endregion

                        break;
                    #endregion


                    #region Assassin
                    case MirClass.Assassin:

                        #region WeaponType
                        if (HasClassWeapon || Weapon < 0)
                        {
                            switch (CurrentAction)
                            {
                                case MirAction.Standing:
                                case MirAction.Stance:
                                case MirAction.Walking:
                                case MirAction.Running:
                                case MirAction.Die:
                                case MirAction.Struck:
                                case MirAction.Attack1:
                                case MirAction.Attack2:
                                case MirAction.Attack3:
                                case MirAction.Attack4:
                                case MirAction.Sneek:
                                case MirAction.Spell:
                                case MirAction.DashAttack:
                                    altAnim = true;
                                    break;
                            }
                        }
                        #endregion

                        #region Armours
                        if (altAnim)
                        {
                            switch (Armour)
                            {
                                case 9: //heaven
                                case 10: //mir
                                case 11: //oma
                                case 12: //spirit
                                    BodyLibrary = Armour + 3 < Libraries.AArmours.Length ? Libraries.AArmours[Armour + 3] : Libraries.AArmours[0];
                                    break;

                                case 19:
                                    BodyLibrary = Armour - 3 < Libraries.AArmours.Length ? Libraries.AArmours[Armour - 3] : Libraries.AArmours[0];
                                    break;

                                case 20:
                                case 21:
                                case 22:
                                case 23: //red bone
                                case 24:
                                    BodyLibrary = Armour - 17 < Libraries.AArmours.Length ? Libraries.AArmours[Armour - 17] : Libraries.AArmours[0];
                                    break;

                                case 28:
                                case 29:
                                case 30:
                                    BodyLibrary = Armour - 20 < Libraries.AArmours.Length ? Libraries.AArmours[Armour - 20] : Libraries.AArmours[0];
                                    break;

                                case 34:
                                    BodyLibrary = Armour - 23 < Libraries.AArmours.Length ? Libraries.AArmours[Armour - 23] : Libraries.AArmours[0];
                                    break;

                                default:
                                    BodyLibrary = Armour < Libraries.AArmours.Length ? Libraries.AArmours[Armour] : Libraries.AArmours[0];
                                    break;
                            }

                            HairLibrary = Hair < Libraries.AHair.Length ? Libraries.AHair[Hair] : null;
                        }
                        else
                        {
                            BodyLibrary = Armour < Libraries.CArmours.Length ? Libraries.CArmours[Armour] : Libraries.CArmours[0];
                            HairLibrary = Hair < Libraries.CHair.Length ? Libraries.CHair[Hair] : null;
                        }
                        #endregion

                        #region Weapons
                        if (HasClassWeapon)
                        {
                            int Index = Weapon - 100;

                            WeaponLibrary1 = Index < Libraries.AWeaponsL.Length ? Libraries.AWeaponsR[Index] : null;
                            WeaponLibrary2 = Index < Libraries.AWeaponsR.Length ? Libraries.AWeaponsL[Index] : null;
                        }
                        else
                        {
                            if (Weapon >= 0)
                                WeaponLibrary1 = Weapon < Libraries.CWeapons.Length ? Libraries.CWeapons[Weapon] : null;
                            else
                                WeaponLibrary1 = null;

                            WeaponLibrary2 = null;
                        }
                        #endregion

                        #region WingEffects
                        if (WingEffect > 0 && WingEffect < 100)
                        {
                            if (altAnim)
                                WingLibrary = (WingEffect - 1) < Libraries.AHumEffect.Length ? Libraries.AHumEffect[WingEffect - 1] : null;
                            else
                                WingLibrary = (WingEffect - 1) < Libraries.CHumEffect.Length ? Libraries.CHumEffect[WingEffect - 1] : null;
                        }
                        #endregion

                        #region Offsets
                        ArmourOffSet = Gender == MirGender.Male ? 0 : altAnim ? 512 : 808;
                        HairOffSet = Gender == MirGender.Male ? 0 : altAnim ? 512 : 808;
                        WeaponOffSet = Gender == MirGender.Male ? 0 : altAnim ? 512 : 416;
                        WingOffset = Gender == MirGender.Male ? 0 : altAnim ? 544 : 840;
                        MountOffset = 0;
                        #endregion

                        break;
                    #endregion


                    #region Others
                    case MirClass.Warrior:
                    case MirClass.Taoist:
                    case MirClass.Wizard:

                        #region Armours
                        BodyLibrary = Armour < Libraries.CArmours.Length ? Libraries.CArmours[Armour] : Libraries.CArmours[0];
                        HairLibrary = Hair < Libraries.CHair.Length ? Libraries.CHair[Hair] : null;
                        #endregion

                        #region Weapons
                        if (Weapon >= 0)
                            WeaponLibrary1 = Weapon < Libraries.CWeapons.Length ? Libraries.CWeapons[Weapon] : null;
                        else
                            WeaponLibrary1 = null;
                        WeaponLibrary2 = null;

                        #endregion

                        #region WingEffects
                        if (WingEffect > 0 && WingEffect < 100)
                        {
                            WingLibrary = (WingEffect - 1) < Libraries.CHumEffect.Length ? Libraries.CHumEffect[WingEffect - 1] : null;
                        }
                        #endregion

                        #region Offsets
                        ArmourOffSet = Gender == MirGender.Male ? 0 : 808;
                        HairOffSet = Gender == MirGender.Male ? 0 : 808;
                        WeaponOffSet = Gender == MirGender.Male ? 0 : 416;
                        WingOffset = Gender == MirGender.Male ? 0 : 840;
                        MountOffset = 0;
                        #endregion

                        break;
                    #endregion
                }
            }

            #region Common
            //Harvest
            if (CurrentAction == MirAction.Harvest && TransformType < 0)
            {
                WeaponLibrary1 = 1 < Libraries.CWeapons.Length ? Libraries.CWeapons[1] : null;
            }

            //Mounts
            if (MountType > -1 && RidingMount && showMount)
            {
                MountLibrary = MountType < Libraries.Mounts.Length ? Libraries.Mounts[MountType] : null;
            }
            else
            {
                MountLibrary = null;
            }

            //Fishing
            if (HasFishingRod && showFishing)
            {
                if (CurrentAction == MirAction.FishingCast || CurrentAction == MirAction.FishingWait || CurrentAction == MirAction.FishingReel)
                {
                    WeaponLibrary1 = 0 < Libraries.Fishing.Length ? Libraries.Fishing[Weapon - 49] : null;
                    WeaponLibrary2 = null;
                    WeaponOffSet = -632;
                }
            }

            DieSound = Gender == MirGender.Male ? SoundList.MaleDie : SoundList.FemaleDie;
            FlinchSound = Gender == MirGender.Male ? SoundList.MaleFlinch : SoundList.FemaleFlinch;
            #endregion
        }

        public virtual void SetEffects()
        {
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                if (Effects[i] is SpecialEffect) Effects[i].Remove();
            }

            if (RidingMount) return;

            if (WingEffect >= 100)
            {
                switch(WingEffect)
                {
                    case 100: //Oma King Robe effect
                        Effects.Add(new SpecialEffect(Libraries.Effect, 352, 33, 3600, this, true, false, 0) { Repeat = true });
                        break;
                }
            }

            long delay = 5000;

            if (LevelEffects == LevelEffects.None) return;

            //Effects dependant on flags
            if (LevelEffects.HasFlag(LevelEffects.BlueDragon))
            {
                Effects.Add(new SpecialEffect(Libraries.Effect, 1210, 20, 3200, this, true, true, 1) { Repeat = true });
                SpecialEffect effect = new SpecialEffect(Libraries.Effect, 1240, 32, 4200, this, true, false, 1) { Repeat = true, Delay = delay };
                effect.SetStart(CMain.Time + delay);
                Effects.Add(effect);
            }
            if (LevelEffects.HasFlag(LevelEffects.RedDragon))
            {
                Effects.Add(new SpecialEffect(Libraries.Effect, 990, 20, 3200, this, true, true, 1) { Repeat = true });
                SpecialEffect effect = new SpecialEffect(Libraries.Effect, 1020, 32, 4200, this, true, false, 1) { Repeat = true, Delay = delay };
                effect.SetStart(CMain.Time + delay);
                Effects.Add(effect);
            }
            if (LevelEffects.HasFlag(LevelEffects.Mist))
            {
                Effects.Add(new SpecialEffect(Libraries.Effect, 296, 32, 3600, this, true, false, 1) { Repeat = true });
            }
        }

        public override void Process()
        {
            bool update = CMain.Time >= NextMotion || GameScene.CanMove;

            if (this == User)
            {
                if (CMain.Time - GameScene.LastRunTime > 699)
                    GameScene.CanRun = false;
            }

            SkipFrames = this != User && ActionFeed.Count > 1;

            ProcessFrames();

            if (Frame == null)
            {
                DrawFrame = 0;
                DrawWingFrame = 0;
            }
            else
            {
                DrawFrame = Frame.Start + (Frame.OffSet * (byte)Direction) + FrameIndex;
                DrawWingFrame = Frame.EffectStart + (Frame.EffectOffSet * (byte)Direction) + EffectFrameIndex;
            }

            #region Moving OffSet

            switch (CurrentAction)
            {
                case MirAction.Walking:
                case MirAction.Running:
                case MirAction.MountWalking:
                case MirAction.MountRunning:
                case MirAction.Pushed:
                case MirAction.DashL:
                case MirAction.DashR:
                case MirAction.Sneek:
                case MirAction.Jump:
                case MirAction.DashAttack:
                    if (Frame == null)
                    {
                        OffSetMove = Point.Empty;
                        Movement = CurrentLocation;
                        break;
                    }

                    var i = 0;
                    if (CurrentAction == MirAction.MountRunning) i = 3;
                    else if (CurrentAction == MirAction.Running) 
                        i = (Sprint && !Sneaking ? 3 : 2);
                    else i = 1;

                    if (CurrentAction == MirAction.Jump) i = -JumpDistance;
                    if (CurrentAction == MirAction.DashAttack) i = JumpDistance;

                    Movement = Functions.PointMove(CurrentLocation, Direction, CurrentAction == MirAction.Pushed ? 0 : -i);

                    int count = Frame.Count;
                    int index = FrameIndex;

                    if (CurrentAction == MirAction.DashR || CurrentAction == MirAction.DashL)
                    {
                        count = 3;
                        index %= 3;
                    }

                    switch (Direction)
                    {
                        case MirDirection.Up:
                            OffSetMove = new Point(0, (int)((MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                        case MirDirection.UpRight:
                            OffSetMove = new Point((int)((-MapControl.CellWidth * i / (float)(count)) * (index + 1)), (int)((MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                        case MirDirection.Right:
                            OffSetMove = new Point((int)((-MapControl.CellWidth * i / (float)(count)) * (index + 1)), 0);
                            break;
                        case MirDirection.DownRight:
                            OffSetMove = new Point((int)((-MapControl.CellWidth * i / (float)(count)) * (index + 1)), (int)((-MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                        case MirDirection.Down:
                            OffSetMove = new Point(0, (int)((-MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                        case MirDirection.DownLeft:
                            OffSetMove = new Point((int)((MapControl.CellWidth * i / (float)(count)) * (index + 1)), (int)((-MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                        case MirDirection.Left:
                            OffSetMove = new Point((int)((MapControl.CellWidth * i / (float)(count)) * (index + 1)), 0);
                            break;
                        case MirDirection.UpLeft:
                            OffSetMove = new Point((int)((MapControl.CellWidth * i / (float)(count)) * (index + 1)), (int)((MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                    }

                    OffSetMove = new Point(OffSetMove.X % 2 + OffSetMove.X, OffSetMove.Y % 2 + OffSetMove.Y);
                    break;
                default:
                    OffSetMove = Point.Empty;
                    Movement = CurrentLocation;
                    break;
            }

            #endregion


            DrawY = Movement.Y > CurrentLocation.Y ? Movement.Y : CurrentLocation.Y;

            DrawLocation = new Point((Movement.X - User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth, (Movement.Y - User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
            DrawLocation.Offset(GlobalDisplayLocationOffset);

            if (this != User)
            {
                DrawLocation.Offset(User.OffSetMove);
                DrawLocation.Offset(-OffSetMove.X, -OffSetMove.Y);
            }

            if (BodyLibrary != null && update)
            {
                FinalDrawLocation = DrawLocation.Add(BodyLibrary.GetOffSet(DrawFrame));
                DisplayRectangle = new Rectangle(DrawLocation, BodyLibrary.GetTrueSize(DrawFrame));
            }

            for (int i = 0; i < Effects.Count; i++)
                Effects[i].Process();

            Color colour = DrawColour;
            DrawColour = Color.White;
            if (Poison != PoisonType.None)
            {
                
                if (Poison.HasFlag(PoisonType.Green))
                    DrawColour = Color.Green;
                if (Poison.HasFlag(PoisonType.Red))
                    DrawColour = Color.Red;
                if (Poison.HasFlag(PoisonType.Bleeding))
                    DrawColour = Color.DarkRed;
                if (Poison.HasFlag(PoisonType.Slow))
                    DrawColour = Color.Purple;
                if (Poison.HasFlag(PoisonType.Stun))
                    DrawColour = Color.Yellow;
                if (Poison.HasFlag(PoisonType.Frozen))
                    DrawColour = Color.Blue;
                if (Poison.HasFlag(PoisonType.Paralysis))
                    DrawColour = Color.Gray;
                if (Poison.HasFlag(PoisonType.DelayedExplosion))
                    DrawColour = Color.Orange;
            }


            if (colour != DrawColour) GameScene.Scene.MapControl.TextureValid = false;
        }
        public virtual void SetAction()
        {
            if (NextAction != null && !GameScene.CanMove)
            {
                switch (NextAction.Action)
                {
                    case MirAction.Walking:
                    case MirAction.Running:
                    case MirAction.MountWalking:
                    case MirAction.MountRunning:
                    case MirAction.Pushed:
                    case MirAction.DashL:
                    case MirAction.DashR:
                    case MirAction.Sneek:
                    case MirAction.Jump:
                    case MirAction.DashAttack:
                        return;
                }
            }

            if (User == this && CMain.Time < MapControl.NextAction)// && CanSetAction)
            {
                //NextMagic = null;
                return;
            }

            if (ActionFeed.Any())
                if (User.RidingMount)
                    switch (ActionFeed.First().Action)
                    {
                        case MirAction.Spell:
                        //case MirAction.Attack1:
                        case MirAction.Attack2:
                        case MirAction.Attack3:
                        case MirAction.Attack4:
                        case MirAction.AttackRange1:
                        case MirAction.AttackRange2:
                        case MirAction.Mine:
                        case MirAction.Harvest:
                            ActionFeed.RemoveAt(0);
                            return;
                    }


            if (ActionFeed.Count == 0)
            {
                CurrentAction = MirAction.Standing;

                CurrentAction = CMain.Time > BlizzardStopTime ? CurrentAction : MirAction.Stance2;
                //CurrentAction = CMain.Time > SlashingBurstTime ? CurrentAction : MirAction.Lunge;

                if (RidingMount)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.Standing:
                            CurrentAction = MirAction.MountStanding;
                            break;
                        case MirAction.Walking:
                            CurrentAction = MirAction.MountWalking;
                            break;
                        case MirAction.Running:
                            CurrentAction = MirAction.MountRunning;
                            break;
                        case MirAction.Struck:
                            CurrentAction = MirAction.MountStruck;
                            break;
                        case MirAction.Attack1:
                            CurrentAction = MirAction.MountAttack;
                            break;
                    }
                }

                if (CurrentAction == MirAction.Standing)
                {
                    if (Class == MirClass.Archer && HasClassWeapon)
                        CurrentAction = MirAction.Standing;
                    else
                        CurrentAction = CMain.Time > StanceTime ? MirAction.Standing : MirAction.Stance;

                    if (Concentrating && ConcentrateInterrupted)
                        Network.Enqueue(new C.SetConcentration { ObjectID = User.ObjectID, Enabled = Concentrating, Interrupted = false });
                }

                if (Fishing) CurrentAction = MirAction.FishingWait;

                Frames.Frames.TryGetValue(CurrentAction, out Frame);
                FrameIndex = 0;
                EffectFrameIndex = 0;

                if (MapLocation != CurrentLocation)
                {
                    GameScene.Scene.MapControl.RemoveObject(this);
                    MapLocation = CurrentLocation;
                    GameScene.Scene.MapControl.AddObject(this);
                }

                if (Frame == null) return;

                FrameInterval = Frame.Interval;
                EffectFrameInterval = Frame.EffectInterval;

                SetLibraries();
            }
            else
            {
                QueuedAction action = ActionFeed[0];
                ActionFeed.RemoveAt(0);


                CurrentAction = action.Action;

                if (RidingMount)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.Standing:
                            CurrentAction = MirAction.MountStanding;
                            break;
                        case MirAction.Walking:
                            CurrentAction = MirAction.MountWalking;
                            break;
                        case MirAction.Running:
                            CurrentAction = MirAction.MountRunning;
                            break;
                        case MirAction.Struck:
                            CurrentAction = MirAction.MountStruck;
                            break;
                        case MirAction.Attack1:
                            CurrentAction = MirAction.MountAttack;
                            break;
                    }
                }

                CurrentLocation = action.Location;
                MirDirection olddirection = Direction;
                Direction = action.Direction;

                Point temp;
                switch (CurrentAction)
                {
                    case MirAction.Walking:
                    case MirAction.Running:
                    case MirAction.MountWalking:
                    case MirAction.MountRunning:
                    case MirAction.Pushed:
                    case MirAction.DashL:
                    case MirAction.DashR:
                    case MirAction.Sneek:
                        var steps = 0;
                        if (CurrentAction == MirAction.MountRunning) steps = 3;
                        else if (CurrentAction == MirAction.Running) steps = (Sprint && !Sneaking ? 3 : 2);
                        else steps = 1;

                        temp = Functions.PointMove(CurrentLocation, Direction, CurrentAction == MirAction.Pushed ? 0 : -steps);

                        break;
                    case MirAction.Jump:
                    case MirAction.DashAttack:
                        temp = Functions.PointMove(CurrentLocation, Direction, JumpDistance);
                        break;
                    default:
                        temp = CurrentLocation;
                        break;
                }

                temp = new Point(action.Location.X, temp.Y > CurrentLocation.Y ? temp.Y : CurrentLocation.Y);

                if (MapLocation != temp)
                {
                    GameScene.Scene.MapControl.RemoveObject(this);
                    MapLocation = temp;
                    GameScene.Scene.MapControl.AddObject(this);
                }


                bool ArcherLayTrap = false;

                switch (CurrentAction)
                {
                    case MirAction.Pushed:
                        if (this == User)
                            MapControl.InputDelay = CMain.Time + 500;
                        Frames.Frames.TryGetValue(MirAction.Walking, out Frame);
                        break;
                    case MirAction.DashL:
                    case MirAction.DashR:
                        Frames.Frames.TryGetValue(MirAction.Running, out Frame);
                        break;
                    case MirAction.DashAttack:
                        Frames.Frames.TryGetValue(MirAction.DashAttack, out Frame);
                        break;
                    case MirAction.DashFail:
                        Frames.Frames.TryGetValue(RidingMount ? MirAction.MountStanding : MirAction.Standing, out Frame);
                        //Frames.Frames.TryGetValue(MirAction.Standing, out Frame);
                        //CanSetAction = false;
                        break;
                    case MirAction.Jump:
                        Frames.Frames.TryGetValue(MirAction.Jump, out Frame);
                        break;
                    case MirAction.Attack1:
                        switch (Class)
                        {
                            case MirClass.Archer:
                                Frames.Frames.TryGetValue(CurrentAction, out Frame);
                                break;
                            case MirClass.Assassin:
                                if(GameScene.DoubleSlash)
                                    Frames.Frames.TryGetValue(MirAction.Attack1, out Frame);
                                else if (CMain.Shift)
                                    Frames.Frames.TryGetValue(CMain.Random.Next(100) >= 20 ? (CMain.Random.Next(100) > 40 ? MirAction.Attack1 : MirAction.Attack4) : (CMain.Random.Next(100) > 10 ? MirAction.Attack2 : MirAction.Attack3), out Frame);
                                else
                                    Frames.Frames.TryGetValue(CMain.Random.Next(100) >= 40 ? MirAction.Attack1 : MirAction.Attack4, out Frame);
                                break;
                            default:
                                if (CMain.Shift && TargetObject == null)
                                    Frames.Frames.TryGetValue(CMain.Random.Next(100) >= 20 ? MirAction.Attack1 : MirAction.Attack3, out Frame);
                                else
                                    Frames.Frames.TryGetValue(CurrentAction, out Frame);
                                break;
                        }
                        break;
                    case MirAction.Attack4:
                        Spell = (Spell)action.Params[0];
                        Frames.Frames.TryGetValue(Spell == Spell.TwinDrakeBlade || Spell == Spell.FlamingSword ? MirAction.Attack1 : CurrentAction, out Frame);
                        break;
                    case MirAction.Spell:
                        Spell = (Spell)action.Params[0];
                        switch (Spell)
                        {
                            case Spell.ShoulderDash:
                                Frames.Frames.TryGetValue(MirAction.Running, out Frame);
                                CurrentAction = MirAction.DashL;
                                Direction = olddirection;
                                CurrentLocation = Functions.PointMove(CurrentLocation, Direction, 1);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2500;
                                    GameScene.SpellTime = CMain.Time + 2500; //Spell Delay

                                    Network.Enqueue(new C.Magic { Spell = Spell, Direction = Direction, });
                                }
                                break;
                            case Spell.BladeAvalanche:
                                Frames.Frames.TryGetValue(MirAction.Attack3, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2500;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.SlashingBurst:
                                 Frames.Frames.TryGetValue(MirAction.Attack1, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2000; // 80%
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.CounterAttack:
                                Frames.Frames.TryGetValue(MirAction.Attack1, out Frame);
                                if (this == User)
                                {
                                    GameScene.AttackTime = CMain.Time + User.AttackSpeed;
                                    MapControl.NextAction = CMain.Time + 100; // 80%
                                    GameScene.SpellTime = CMain.Time + 100; //Spell Delay
                                }
                                break;
                            case Spell.PoisonSword:
                                Frames.Frames.TryGetValue(MirAction.Attack1, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2000; // 80%
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.HeavenlySword:
                                Frames.Frames.TryGetValue(MirAction.Attack2, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1200;
                                    GameScene.SpellTime = CMain.Time + 1200; //Spell Delay
                                }
                                break;
                            case Spell.CrescentSlash:
                                Frames.Frames.TryGetValue(MirAction.Attack3, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2500;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.FlashDash:
                                {
                                    int sLevel = (byte)action.Params[3];

                                    GetFlashDashDistance(sLevel);

                                    if (JumpDistance != 0)
                                    {
                                        Frames.Frames.TryGetValue(MirAction.DashAttack, out Frame);
                                        CurrentAction = MirAction.DashAttack;
                                        CurrentLocation = Functions.PointMove(CurrentLocation, Direction, JumpDistance);
                                    }
                                    else
                                    {
                                        Frames.Frames.TryGetValue(CMain.Random.Next(100) >= 40 ? MirAction.Attack1 : MirAction.Attack4, out Frame);
                                    }

                                    if (this == User)
                                    {
                                        MapControl.NextAction = CMain.Time;
                                        GameScene.SpellTime = CMain.Time + 250; //Spell Delay
                                        if (JumpDistance != 0) Network.Enqueue(new C.Magic { Spell = Spell, Direction = Direction });
                                    }
                                }
                                break;
                            case Spell.StraightShot:
                                Frames.Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.DoubleShot:                          
                                Frames.Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 500; //Spell Delay
                                }
                                break;
                            case Spell.ExplosiveTrap:
                                Frames.Frames.TryGetValue(MirAction.Harvest, out Frame);
                                CurrentAction = MirAction.Harvest;
                                ArcherLayTrap = true;
                                if (this == User)
                                {
                                    uint targetID = (uint)action.Params[1];
                                    Point location = (Point)action.Params[2];
                                    Network.Enqueue(new C.Magic { Spell = Spell, Direction = Direction, TargetID = targetID, Location = location });
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.DelayedExplosion:
                                Frames.Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.BackStep:
                                {
                                    int sLevel = (byte)action.Params[3];
                                    GetBackStepDistance(sLevel);
                                    Frames.Frames.TryGetValue(MirAction.Jump, out Frame);
                                    CurrentAction = MirAction.Jump;
                                    CurrentLocation = Functions.PointMove(CurrentLocation, Functions.ReverseDirection(Direction), JumpDistance);
                                    if (this == User)
                                    {
                                        MapControl.NextAction = CMain.Time + 800;
                                        GameScene.SpellTime = CMain.Time + 2500; //Spell Delay
                                        Network.Enqueue(new C.Magic { Spell = Spell, Direction = Direction });
                                    }
                                    break;
                                }
                            case Spell.ElementalShot:
                                if (HasElements && !ElementCasted)
                                {
                                    Frames.Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                    CurrentAction = MirAction.AttackRange2;
                                    if (this == User)
                                    {
                                        MapControl.NextAction = CMain.Time + 1000;
                                        GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                    }
                                }
                                else Frames.Frames.TryGetValue(CurrentAction, out Frame);
                                if (ElementCasted) ElementCasted = false;
                                break;
                            case Spell.BindingShot:
                            case Spell.VampireShot:
                            case Spell.PoisonShot:
                            case Spell.CrippleShot:
                            case Spell.NapalmShot:
                            case Spell.SummonVampire:
                            case Spell.SummonToad:
                            case Spell.SummonSnakes:
                                Frames.Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1000; //Spell Delay
                                }
                                break;
                            default:
                                Frames.Frames.TryGetValue(CurrentAction, out Frame);
                                break;
                        }
                        
                        break;
                    default:
                        Frames.Frames.TryGetValue(CurrentAction, out Frame);
                        break;

                }

                //ArcherTest - Need to check for bow weapon only
                if (Class == MirClass.Archer && HasClassWeapon)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.Walking:
                            Frames.Frames.TryGetValue(MirAction.WalkingBow, out Frame);
                            break;
                        case MirAction.Running:
                            Frames.Frames.TryGetValue(MirAction.RunningBow, out Frame);
                            break;
                    }
                }

                //Assassin sneekyness
                if (Class == MirClass.Assassin && Sneaking && (CurrentAction == MirAction.Walking || CurrentAction == MirAction.Running))
                {
                    Frames.Frames.TryGetValue(MirAction.Sneek, out Frame);
                }

                SetLibraries();

                FrameIndex = 0;
                EffectFrameIndex = 0;
                Spell = Spell.None;
                SpellLevel = 0;
                //NextMagic = null;

                ClientMagic magic;

                if (Frame == null) return;

                FrameInterval = Frame.Interval;
                EffectFrameInterval = Frame.EffectInterval;

                if (this == User)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.DashFail:
                            //CanSetAction = false;
                            break;
                        case MirAction.Standing:
                        case MirAction.MountStanding:
                            Network.Enqueue(new C.Turn { Direction = Direction });
                            MapControl.NextAction = CMain.Time + 2500;
                            GameScene.CanRun = false;
                            break;
                        case MirAction.Walking:
                        case MirAction.MountWalking:
                        case MirAction.Sneek:
                            GameScene.LastRunTime = CMain.Time;
                            Network.Enqueue(new C.Walk { Direction = Direction });
                            GameScene.Scene.MapControl.FloorValid = false;
                            GameScene.CanRun = true;
                            MapControl.NextAction = CMain.Time + 2500;
                            break;
                        case MirAction.Running:
                        case MirAction.MountRunning:
                            GameScene.LastRunTime = CMain.Time;
                            Network.Enqueue(new C.Run { Direction = Direction });
                            GameScene.Scene.MapControl.FloorValid = false;
                            MapControl.NextAction = CMain.Time + (Sprint ? 1000 : 2500);
                            break;
                        case MirAction.Pushed:
                            GameScene.LastRunTime = CMain.Time;
                            GameScene.Scene.MapControl.FloorValid = false;
                            MapControl.InputDelay = CMain.Time + 500;
                            break;
                        case MirAction.DashL:
                        case MirAction.DashR:
                        case MirAction.Jump:
                        case MirAction.DashAttack:
                            GameScene.LastRunTime = CMain.Time;
                            GameScene.Scene.MapControl.FloorValid = false;
                            GameScene.CanRun = false;
                            //CanSetAction = false;
                            break;
                        case MirAction.Mine:
                            Network.Enqueue(new C.Attack { Direction = Direction, Spell = Spell.None });
                            GameScene.AttackTime = CMain.Time + (1400 - Math.Min(370, (User.Level * 14)));
                            MapControl.NextAction = CMain.Time + 2500;
                            break;
                        case MirAction.Attack1:
                        case MirAction.MountAttack:

                            if (!RidingMount)
                            {
                                if (GameScene.Slaying && TargetObject != null)
                                    Spell = Spell.Slaying;

                                if (GameScene.Thrusting && GameScene.Scene.MapControl.HasTarget(Functions.PointMove(CurrentLocation, Direction, 2)))
                                    Spell = Spell.Thrusting;

                                if (GameScene.HalfMoon)
                                {
                                    if (TargetObject != null || GameScene.Scene.MapControl.CanHalfMoon(CurrentLocation, Direction))
                                    {
                                        magic = User.GetMagic(Spell.HalfMoon);
                                        if (magic != null && magic.BaseCost + magic.LevelCost * magic.Level <= User.MP)
                                            Spell = Spell.HalfMoon;
                                    }
                                }

                                if (GameScene.CrossHalfMoon)
                                {
                                    if (TargetObject != null || GameScene.Scene.MapControl.CanCrossHalfMoon(CurrentLocation))
                                    {
                                        magic = User.GetMagic(Spell.CrossHalfMoon);
                                        if (magic != null && magic.BaseCost + magic.LevelCost * magic.Level <= User.MP)
                                            Spell = Spell.CrossHalfMoon;
                                    }
                                }

                                if (GameScene.DoubleSlash)
                                {
                                    magic = User.GetMagic(Spell.DoubleSlash);
                                    if (magic != null && magic.BaseCost + magic.LevelCost * magic.Level <= User.MP)
                                        Spell = Spell.DoubleSlash;
                                }


                                if (GameScene.TwinDrakeBlade && TargetObject != null)
                                {
                                    magic = User.GetMagic(Spell.TwinDrakeBlade);
                                    if (magic != null && magic.BaseCost + magic.LevelCost * magic.Level <= User.MP)
                                        Spell = Spell.TwinDrakeBlade;
                                }

                                if (GameScene.FlamingSword)
                                {
                                    if (TargetObject != null)
                                    {
                                        magic = User.GetMagic(Spell.FlamingSword);
                                        if (magic != null)
                                            Spell = Spell.FlamingSword;
                                    }
                                }
                            }

                            Network.Enqueue(new C.Attack { Direction = Direction, Spell = Spell });

                            if (Spell == Spell.Slaying)
                                GameScene.Slaying = false;
                            if (Spell == Spell.TwinDrakeBlade)
                                GameScene.TwinDrakeBlade = false;
                            if (Spell == Spell.FlamingSword)
                                GameScene.FlamingSword = false;

                            magic = User.GetMagic(Spell);

                            if (magic != null) SpellLevel = magic.Level;


                            GameScene.AttackTime = CMain.Time + User.AttackSpeed;
                            MapControl.NextAction = CMain.Time + 2500;
                            break;
                        case MirAction.Attack2:
                            //Network.Enqueue(new C.Attack2 { Direction = Direction });
                            break;
                        case MirAction.Attack3:
                            //Network.Enqueue(new C.Attack3 { Direction = Direction });
                            break;
                        //case MirAction.Attack4:
                        //    GameScene.AttackTime = CMain.Time;// + User.AttackSpeed;
                        //    MapControl.NextAction = CMain.Time;
                        //    break;

                        case MirAction.AttackRange1: //ArcherTest
                            GameScene.AttackTime = CMain.Time + User.AttackSpeed + 200;

                            uint targetID = (uint)action.Params[0];
                            Point location = (Point)action.Params[1];

                            Network.Enqueue(new C.RangeAttack { Direction = Direction, Location = CurrentLocation, TargetID = targetID, TargetLocation = location });
                            break;
                        case MirAction.AttackRange2:
                        case MirAction.Spell:
                            Spell = (Spell)action.Params[0];
                            targetID = (uint)action.Params[1];
                            location = (Point)action.Params[2];

                            //magic = User.GetMagic(Spell);
                            //magic.LastCast = CMain.Time;

                            Network.Enqueue(new C.Magic { Spell = Spell, Direction = Direction, TargetID = targetID, Location = location });

                            if (Spell == Spell.FlashDash)
                            {
                                GameScene.SpellTime = CMain.Time + 250;
                                MapControl.NextAction = CMain.Time;
                            }
                            else
                            {
                                GameScene.SpellTime = Spell == Spell.FlameField ? CMain.Time + 2500 : CMain.Time + 1800;
                                MapControl.NextAction = CMain.Time + 2500;
                            }
                            break;
                        case MirAction.Harvest:
                            if (ArcherLayTrap)
                            {
                                ArcherLayTrap = false;
                                SoundManager.PlaySound(20000 + 124 * 10);
                            }
                            else
                            {
                                Network.Enqueue(new C.Harvest { Direction = Direction });
                                MapControl.NextAction = CMain.Time + 2500;
                            }
                            break;

                    }
                }


                switch (CurrentAction)
                {
                    case MirAction.Pushed:
                        FrameIndex = Frame.Count - 1;
                        EffectFrameIndex = Frame.EffectCount - 1;
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.DashL:
                    case MirAction.Jump:
                        FrameIndex = 0;
                        EffectFrameIndex = 0;
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.DashR:
                        FrameIndex = 3;
                        EffectFrameIndex = 3;
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.Walking:
                    case MirAction.Running:
                    case MirAction.MountWalking:
                    case MirAction.MountRunning:
                    case MirAction.Sneek:
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.DashAttack:
                        //FrameIndex = 0;
                        //EffectFrameIndex = 0;
                        GameScene.Scene.Redraw();

                        if (IsDashAttack())
                        {
                            action = new QueuedAction { Action = MirAction.Attack4, Direction = Direction, Location = CurrentLocation, Params = new List<object>() };
                            action.Params.Add(Spell.FlashDash);
                            ActionFeed.Insert(0, action);
                        }
                        break;
                    case MirAction.Attack1:
                        if (this != User)
                        {
                            Spell = (Spell)action.Params[0];
                            SpellLevel = (byte)action.Params[1];
                        }

                        switch (Spell)
                        {
                            case Spell.Slaying:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));
                                break;
                            case Spell.DoubleSlash:
                                FrameInterval = (FrameInterval * 7 / 10); //50% Faster Animation
                                EffectFrameInterval = (EffectFrameInterval * 7 / 10);
                                action = new QueuedAction { Action = MirAction.Attack4, Direction = Direction, Location = CurrentLocation, Params = new List<object>() };
                                action.Params.Add(Spell);
                                ActionFeed.Insert(0, action);
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;
                            case Spell.Thrusting:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;
                            case Spell.HalfMoon:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.TwinDrakeBlade:
                                //FrameInterval = FrameInterval * 9 / 10; //70% Faster Animation
                                //EffectFrameInterval = EffectFrameInterval * 9 / 10;
                                //action = new QueuedAction { Action = MirAction.Attack4, Direction = Direction, Location = CurrentLocation, Params = new List<object>() };
                                //action.Params.Add(Spell);
                                //ActionFeed.Insert(0, action);
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.CrossHalfMoon:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.FlamingSword:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                break;

                            
                        }
                        break;
                    case MirAction.Attack4:
                        Spell = (Spell)action.Params[0];
                        switch (Spell)
                        {
                            case Spell.DoubleSlash:
                                FrameInterval = FrameInterval * 7 / 10; //50% Animation Speed
                                EffectFrameInterval = EffectFrameInterval * 7 / 10;
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                break;
                            case Spell.TwinDrakeBlade:
                                FrameInterval = FrameInterval * 9 / 10; //80% Animation Speed
                                EffectFrameInterval = EffectFrameInterval * 9 / 10;
                                break;
                            case Spell.FlashDash:
                                int attackDelay = (User.AttackSpeed - 120) <= 300 ? 300 : (User.AttackSpeed - 120);

                                float attackRate = (float)(attackDelay / 300F * 10F);
                                FrameInterval = FrameInterval * (int)attackRate / 20;
                                EffectFrameInterval = EffectFrameInterval * (int)attackRate / 20;
                                break;
                        }
                        break;
                    case MirAction.Struck:
                    case MirAction.MountStruck:
                        uint attackerID = (uint)action.Params[0];
                        StruckWeapon = -2;
                        for (int i = 0; i < MapControl.Objects.Count; i++)
                        {
                            MapObject ob = MapControl.Objects[i];
                            if (ob.ObjectID != attackerID) continue;
                            if (ob.Race != ObjectType.Player) break;
                            PlayerObject player = ((PlayerObject)ob);
                            StruckWeapon = player.Weapon;
                            if (player.Class != MirClass.Assassin || StruckWeapon == -1) break;
                            StruckWeapon = 1;
                            break;
                        }

                        PlayStruckSound();
                        PlayFlinchSound();
                        break;
                    case MirAction.AttackRange1: //ArcherTest - Assign Target for other users
                        if (this != User)
                        {
                            TargetID = (uint)action.Params[0];
                            TargetPoint = (Point)action.Params[1];
                            Spell = (Spell)action.Params[2];
                        }
                        break;
                    case MirAction.AttackRange2:
                    case MirAction.Spell:
                        if (this != User)
                        {
                            Spell = (Spell)action.Params[0];
                            TargetID = (uint)action.Params[1];
                            TargetPoint = (Point)action.Params[2];
                            Cast = (bool)action.Params[3];
                            SpellLevel = (byte)action.Params[4];
                        }

                        switch (Spell)
                        {
                            #region FireBall

                            case Spell.FireBall:
                                Effects.Add(new Effect(Libraries.Magic, 0, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Healing

                            case Spell.Healing:
                                Effects.Add(new Effect(Libraries.Magic, 200, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Repulsion

                            case Spell.Repulsion:
                                Effects.Add(new Effect(Libraries.Magic, 900, 6, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region ElectricShock

                            case Spell.ElectricShock:
                                Effects.Add(new Effect(Libraries.Magic, 1560, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Poisoning

                            case Spell.Poisoning:
                                Effects.Add(new Effect(Libraries.Magic, 600, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region GreatFireBall

                            case Spell.GreatFireBall:
                                Effects.Add(new Effect(Libraries.Magic, 400, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region HellFire

                            case Spell.HellFire:
                                Effects.Add(new Effect(Libraries.Magic, 920, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region ThunderBolt

                            case Spell.ThunderBolt:
                                Effects.Add(new Effect(Libraries.Magic2, 20, 3, 300, this));
                                break;

                            #endregion

                            #region SoulFireBall

                            case Spell.SoulFireBall:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region SummonSkeleton

                            case Spell.SummonSkeleton:
                                Effects.Add(new Effect(Libraries.Magic, 1500, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion
                            #region StormEscape
                            case Spell.StormEscape:
                                Effects.Add(new Effect(Libraries.Magic3, 590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;
                            #endregion
                            #region Teleport

                            case Spell.Teleport:
                                Effects.Add(new Effect(Libraries.Magic, 1590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Blink

                            case Spell.Blink:
                                Effects.Add(new Effect(Libraries.Magic, 1590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Hiding

                            case Spell.Hiding:
                                Effects.Add(new Effect(Libraries.Magic, 1520, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Haste

                            case Spell.Haste:
                                Effects.Add(new Effect(Libraries.Magic2, 2140 + (int)Direction * 10, 6, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Fury

                            case Spell.Fury:
                                Effects.Add(new Effect(Libraries.Magic3, 200, 8, 8 * FrameInterval, this));
                                Effects.Add(new Effect(Libraries.Magic3, 187, 10, 10 * FrameInterval, this));
                                //i don't know sound
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region ImmortalSkin
                            case Spell.ImmortalSkin:
                                Effects.Add(new Effect(Libraries.Magic3, 550, 17, Frame.Count * FrameInterval * 4, this));
                                Effects.Add(new Effect(Libraries.Magic3, 570, 5, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;
                            #endregion

                            #region FireBang

                            case Spell.FireBang:
                                Effects.Add(new Effect(Libraries.Magic, 1650, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region FireWall

                            case Spell.FireWall:
                                Effects.Add(new Effect(Libraries.Magic, 1620, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region TrapHexagon

                            case Spell.TrapHexagon:
                                Effects.Add(new Effect(Libraries.Magic, 1380, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region EnergyRepulsor

                            case Spell.EnergyRepulsor:
                                Effects.Add(new Effect(Libraries.Magic2, 190, 6, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region FireBurst

                            case Spell.FireBurst:
                                Effects.Add(new Effect(Libraries.Magic2, 2320, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region FlameDisruptor

                            case Spell.FlameDisruptor:
                                Effects.Add(new Effect(Libraries.Magic2, 130, 6, Frame.Count * FrameInterval, this));
                                break;

                            #endregion

                            #region SummonShinsu

                            case Spell.SummonShinsu:
                                Effects.Add(new Effect(Libraries.Magic2, 0, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region UltimateEnchancer

                            case Spell.UltimateEnhancer:
                                Effects.Add(new Effect(Libraries.Magic2, 160, 15, 1000, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region FrostCrunch

                            case Spell.FrostCrunch:
                                Effects.Add(new Effect(Libraries.Magic2, 400, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Purification

                            case Spell.Purification:
                                Effects.Add(new Effect(Libraries.Magic2, 600, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region FlameField

                            case Spell.FlameField:
                                MapControl.Effects.Add(new Effect(Libraries.Magic2, 910, 23, 1800, CurrentLocation));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Trap

                            case Spell.Trap:
                                Effects.Add(new Effect(Libraries.Magic2, 2340, 11, 11 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region MoonLight

                            case Spell.MoonLight:
                                Effects.Add(new Effect(Libraries.Magic2, 2380, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region SwiftFeet

                            case Spell.SwiftFeet:
                                Effects.Add(new Effect(Libraries.Magic2, 2440, 16, 16 * EffectFrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region LightBody

                            case Spell.LightBody:
                                Effects.Add(new Effect(Libraries.Magic2, 2470, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion


                            #region PoisonSword

                            case Spell.PoisonSword:
                                Effects.Add(new Effect(Libraries.Magic2, 2490 + ((int)Direction * 10), 10, Frame.Count * FrameInterval + 500, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region DarkBody

                            case Spell.DarkBody:
                                Effects.Add(new Effect(Libraries.Magic2, 2580, 10, 10 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region ThunderStorm

                            case Spell.ThunderStorm:
                                MapControl.Effects.Add(new Effect(Libraries.Magic, 1680, 10, Frame.Count * FrameInterval, CurrentLocation));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region MassHealing

                            case Spell.MassHealing:
                                Effects.Add(new Effect(Libraries.Magic, 1790, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region IceStorm

                            case Spell.IceStorm:
                                Effects.Add(new Effect(Libraries.Magic, 3840, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region MagicShield

                            case Spell.MagicShield:
                                Effects.Add(new Effect(Libraries.Magic, 3880, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region TurnUndead

                            case Spell.TurnUndead:
                                Effects.Add(new Effect(Libraries.Magic, 3920, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region MagicBooster

                            case Spell.MagicBooster:
                                Effects.Add(new Effect(Libraries.Magic3, 80, 9, 9 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region PetEnhancer

                            case Spell.PetEnhancer:
                                Effects.Add(new Effect(Libraries.Magic3, 200, 8, 8 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Revelation

                            case Spell.Revelation:
                                Effects.Add(new Effect(Libraries.Magic, 3960, 20, 1200, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region ProtectionField

                            case Spell.ProtectionField:
                                Effects.Add(new Effect(Libraries.Magic2, 1520, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Rage

                            case Spell.Rage:
                                Effects.Add(new Effect(Libraries.Magic2, 1510, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion


                            #region Vampirism

                            case Spell.Vampirism:
                                Effects.Add(new Effect(Libraries.Magic2, 1040, 7, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region LionRoar

                            case Spell.LionRoar:
                                Effects.Add(new Effect(Libraries.Magic2, 710, 20, 1200, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));
                                break;

                            #endregion

                            #region TwinDrakeBlade

                            case Spell.TwinDrakeBlade:
                                Effects.Add(new Effect(Libraries.Magic2, 210, 6, 500, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Entrapment

                            case Spell.Entrapment:
                                Effects.Add(new Effect(Libraries.Magic2, 990, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region BladeAvalanche

                            case Spell.BladeAvalanche:
                                Effects.Add(new Effect(Libraries.Magic2, 740 + (int)Direction * 20, 15, 15 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region SlashingBurst

                            case Spell.SlashingBurst:
                                //MapControl.Effects.Add(new Effect(Libraries.Magic2, 1700 + (int)Direction * 10, 9, 9 * FrameInterval, CurrentLocation));
                                Effects.Add(new Effect(Libraries.Magic2, 1700 + (int)Direction * 10, 9, 9 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                SlashingBurstTime = CMain.Time + 2000;
                                break;

                            #endregion

                            #region CounterAttack

                            case Spell.CounterAttack:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 5);
                                Effects.Add(new Effect(Libraries.Magic, 3480 + (int)Direction * 10, 10, 10 * FrameInterval, this));
                                Effects.Add(new Effect(Libraries.Magic3, 140, 2, 2 * FrameInterval, this));
                                break;

                            #endregion

                            #region CrescentSlash

                            case Spell.CrescentSlash:
                                Effects.Add(new Effect(Libraries.Magic2, 2620 + (int)Direction * 20, 20, 20 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));

                               
                                break;

                            #endregion

                            #region FlashDash

                            case Spell.FlashDash:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));
                                int attackDelay = (User.AttackSpeed - 120) <= 300 ? 300 : (User.AttackSpeed - 120);

                                float attackRate = (float)(attackDelay / 300F * 10F);
                                FrameInterval = FrameInterval * (int)attackRate / 20;
                                EffectFrameInterval = EffectFrameInterval * (int)attackRate / 20;
                                break;
                            #endregion

                            #region Mirroring

                            case Spell.Mirroring:
                                Effects.Add(new Effect(Libraries.Magic2, 650, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region Blizzard

                            case Spell.Blizzard:
                                Effects.Add(new Effect(Libraries.Magic2, 1540, 8, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                BlizzardStopTime = CMain.Time + 3000;
                                break;

                            #endregion

                            #region MeteorStrike

                            case Spell.MeteorStrike:
                                Effects.Add(new Effect(Libraries.Magic2, 1590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                BlizzardStopTime = CMain.Time + 3000;
                                break;

                            #endregion

                            #region Reincarnation

                            case Spell.Reincarnation:
                                ReincarnationStopTime = CMain.Time + 6000;
                                break;

                            #endregion

                            #region HeavenlySword

                            case Spell.HeavenlySword:
                                Effects.Add(new Effect(Libraries.Magic2, 2230 + ((int)Direction * 10), 8, 800, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            #endregion

                            #region ElementalBarrier

                            case Spell.ElementalBarrier:
                                if (HasElements && !ElementalBarrier)
                                {
                                    Effects.Add(new Effect(Libraries.Magic3, 1880, 8, Frame.Count * FrameInterval, this));
                                    SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                }
                                break;

                            #endregion

                            #region PoisonShot
                            case Spell.PoisonShot:
                                Effects.Add(new Effect(Libraries.Magic3, 2300, 8, 1000, this));
                                break;
                            #endregion

                            #region OneWithNature
                            case Spell.OneWithNature:
                                MapControl.Effects.Add(new Effect(Libraries.Magic3, 2710, 8, 1200, CurrentLocation));
                                SoundManager.PlaySound(20000 + 139 * 10);
                                break;
                            #endregion

                        }


                        break;
                    case MirAction.Dead:
                        GameScene.Scene.Redraw();
                        GameScene.Scene.MapControl.SortObject(this);
                        if (MouseObject == this) MouseObject = null;
                        if (TargetObject == this) TargetObject = null;
                        if (MagicObject == this) MagicObject = null;
                        DeadTime = CMain.Time;
                        break;

                }

            }

            GameScene.Scene.MapControl.TextureValid = false;

            NextMotion = CMain.Time + FrameInterval;
            NextMotion2 = CMain.Time + EffectFrameInterval;

            if (ElementalBarrier)
            {
                switch (CurrentAction)
                {
                    case MirAction.Struck:
                    case MirAction.MountStruck:
                        if (ElementalBarrierEffect != null)
                        {
                            ElementalBarrierEffect.Clear();
                            ElementalBarrierEffect.Remove();
                        }

                        Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1910, 5, 600, this));
                        ElementalBarrierEffect.Complete += (o, e) => Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 16, 3200, this) { Repeat = true });
                        break;
                    default:
                        if (ElementalBarrierEffect == null)
                            Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 16, 3200, this) { Repeat = true });
                        break;
                }
            }

            if (MagicShield)
            {
                switch (CurrentAction)
                {
                    case MirAction.Struck:
                    case MirAction.MountStruck:
                        if (ShieldEffect != null)
                        {
                            ShieldEffect.Clear();
                            ShieldEffect.Remove();
                        }

                        Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3900, 3, 600, this));
                        ShieldEffect.Complete += (o, e) => Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, this) { Repeat = true });
                        break;
                    default:
                        if (ShieldEffect == null)
                            Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, this) { Repeat = true });
                        break;
                }
            }

        }

        public virtual void ProcessFrames()
        {
            if (Frame == null) return;
            //thedeath2
            //slow frame speed
            //if (Poison == PoisonType.Slow)
            //{
            //    if (CurrentAction != MirAction.Standing)
            //    {
            //        if (SlowFrameIndex >= 3)
            //        {
            //            SlowFrameIndex = 0;
            //        }
            //        else
            //        {
            //            SlowFrameIndex++;
            //            return;
            //        }
            //    }
            //}
            //else
            //{
            //    SlowFrameIndex = 0;
            //}

            switch (CurrentAction)
            {
                case MirAction.Walking:
                case MirAction.Running:
                case MirAction.MountWalking:
                case MirAction.MountRunning:
                case MirAction.Sneek:
                case MirAction.DashAttack:
                    if (!GameScene.CanMove) return;
                    

                    GameScene.Scene.MapControl.TextureValid = false;

                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;
                    //if (CMain.Time < NextMotion) return;
                    if (SkipFrames) UpdateFrame();



                    if (UpdateFrame(false) >= Frame.Count)
                    {


                        FrameIndex = Frame.Count - 1;
                        SetAction();
                    }
                    else
                    {
                        if (this == User)
                        {
                            if (FrameIndex == 1 || FrameIndex == 4)
                                PlayStepSound();
                        }
                        //NextMotion += FrameInterval;
                    }

                    if (WingEffect > 0 && CMain.Time >= NextMotion2)
                    {
                        if (this == User) GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;
                 case MirAction.Jump:
                    if (!GameScene.CanMove) return;
                    GameScene.Scene.MapControl.TextureValid = false;
                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;
                    if (SkipFrames) UpdateFrame();
                    if (UpdateFrame() >= Frame.Count)
                    {
                        FrameIndex = Frame.Count - 1;
                        SetAction();
                    }
                    else
                    {
                        if (FrameIndex == 1)
                            SoundManager.PlaySound(20000 + 127 * 10 + (Gender == MirGender.Male ? 5 : 6));
                        if (FrameIndex == 7)
                            SoundManager.PlaySound(20000 + 127 * 10 + 7);
                    }
                    //Backstep wingeffect
                    if (WingEffect > 0 && CMain.Time >= NextMotion2)
                    {
                        if (this == User) GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;
                case MirAction.DashL:
                    if (!GameScene.CanMove) return;

                    GameScene.Scene.MapControl.TextureValid = false;

                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;
                    if (UpdateFrame() >= 3)
                    {
                        FrameIndex = 2;
                        SetAction();
                    }

                    if (UpdateFrame2() >= 3) EffectFrameIndex = 2;
                    break;
                case MirAction.DashR:
                    if (!GameScene.CanMove) return;

                    GameScene.Scene.MapControl.TextureValid = false;

                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;

                    if (UpdateFrame() >= 6)
                    {
                        FrameIndex = 5;
                        SetAction();
                    }

                    if (UpdateFrame2() >= 6) EffectFrameIndex = 5;
                    break;
                case MirAction.Pushed:
                    if (!GameScene.CanMove) return;

                    GameScene.Scene.MapControl.TextureValid = false;

                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;

                    FrameIndex -= 2;
                    EffectFrameIndex -= 2;

                    if (FrameIndex < 0)
                    {
                        FrameIndex = 0;
                        SetAction();
                    }

                    if (FrameIndex < 0) EffectFrameIndex = 0;
                    break;

                case MirAction.Standing:
                case MirAction.MountStanding:
                case MirAction.DashFail:
                case MirAction.Harvest:
                case MirAction.Stance:
                case MirAction.Stance2:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }

                    if (WingEffect > 0 && CMain.Time >= NextMotion2)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;  


                case MirAction.FishingCast:             
                case MirAction.FishingReel:
                case MirAction.FishingWait:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            switch (FrameIndex)
                            {
                                case 1:
                                    switch (CurrentAction)
                                    {
                                        case MirAction.FishingCast:
                                            SoundManager.PlaySound(SoundList.FishingThrow);
                                            ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).Visible = false;
                                            break;
                                        case MirAction.FishingReel:
                                            SoundManager.PlaySound(SoundList.FishingPull);
                                            break;
                                        case MirAction.FishingWait:
                                            if (FoundFish)
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 671, 6, 720, FishingPoint) { Light = 0 });
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 665, 6, 720, FishingPoint) { Light = 0 });
                                                SoundManager.PlaySound(SoundList.Fishing);
                                                Effects.Add(new Effect(Libraries.Prguse, 1350, 2, 720, this) { Light = 0 });
                                                ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).Visible = true;
                                            }
                                            else
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 650, 6, 720, FishingPoint) { Light = 0 });
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 640, 6, 720, FishingPoint) { Light = 0 });
                                            }
                                            ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).AnimationCount = FoundFish ? 10 : 1;
                                            break;
                                    }
                                    break;
                            }
                            NextMotion += FrameInterval;
                        }
                    }

                    if (WingEffect > 0 && CMain.Time >= NextMotion2)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;     

                case MirAction.Attack1:
                case MirAction.Attack2:
                case MirAction.Attack3:
                case MirAction.Attack4:
                case MirAction.MountAttack:
                case MirAction.Mine:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            //if (ActionFeed.Count == 0)
                            //    ActionFeed.Add(new QueuedAction { Action = MirAction.Stance, Direction = Direction, Location = CurrentLocation });

                            StanceTime = CMain.Time + StanceDelay;
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            if (FrameIndex == 1) PlayAttackSound();
                            NextMotion += FrameInterval;
                        }
                    }

                    if (WingEffect > 0 && CMain.Time >= NextMotion2)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;

                case MirAction.AttackRange1:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            if (FrameIndex == 1) PlayAttackSound();
                            Missile missile;
                            switch (FrameIndex)
                            {
                                case 6:
                                    switch (Spell)
                                    {
                                        case Spell.Focus:
                                            Effects.Add(new Effect(Libraries.Magic3, 2730, 10, Frame.Count * FrameInterval, this));
                                            SoundManager.PlaySound(20000 + 121 * 10 + 5);
                                            break;
                                    }

                                    break;
                                case 5:
                                    missile = CreateProjectile(1030, Libraries.Magic3, true, 5, 30, 5);
                                    StanceTime = CMain.Time + StanceDelay;
                                    SoundManager.PlaySound(20000 + 121 * 10);
                                    if (missile.Target != null)
                                    {
                                        missile.Complete += (o, e) =>
                                        {
                                            SoundManager.PlaySound(20000 + 121 * 10 + 2);
                                        };
                                    }
                                    break;
                            }

                            NextMotion += FrameInterval;
                        }
                    }

                    if (WingEffect > 0 && CMain.Time >= NextMotion2)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;

                case MirAction.AttackRange2:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            if (Cast)
                            {
                                MapObject ob = MapControl.GetObject(TargetID);

                                Missile missile;
                                switch (Spell)
                                {
                                    case Spell.StraightShot:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 0);
                                        missile = CreateProjectile(1210, Libraries.Magic3, true, 5, 30, 5);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic3, 1370, 7, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.StraightShot * 10 + 2);
                                            };
                                        }
                                        break;
                                }


                                Cast = false;
                            }

                            StanceTime = CMain.Time + StanceDelay;
                            FrameIndex = Frame.Count - 1;
                            SetAction();

                        }
                        else
                        {
                            NextMotion += FrameInterval;

                            Missile missile;

                            switch(Spell)
                            {
                                case Spell.DoubleShot:
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                        case 5:
                                            missile = CreateProjectile(1030, Libraries.Magic3, true, 5, 30, 5);//normal arrow
                                            StanceTime = CMain.Time + StanceDelay;
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + 121 * 10 + 2);
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.ElementalShot:
                                    if (HasElements && !ElementCasted)
                                        switch (FrameIndex)
                                        {
                                            case 7:
                                                missile = CreateProjectile(1690, Libraries.Magic3, true, 6, 30, 4);//elemental arrow
                                                StanceTime = CMain.Time + StanceDelay;
                                                if (missile.Target != null)
                                                {
                                                    missile.Complete += (o, e) =>
                                                    {
                                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);//sound M128-2
                                                    };
                                                }
                                                break;
                                            case 1:
                                                Effects.Add(new Effect(Libraries.Magic3, 1681, 5, Frame.Count * FrameInterval, this));
                                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 0);//sound M128-0
                                                break;
                                        }
                                    break;
                                case Spell.BindingShot:
                                case Spell.SummonVampire:
                                case Spell.SummonToad:
                                case Spell.SummonSnakes:
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            missile = CreateProjectile(2750, Libraries.Magic3, true, 5, 10, 5);
                                            StanceTime = CMain.Time + StanceDelay;
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 7);//sound M130-7
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.DelayedExplosion:
                                    switch (FrameIndex)
                                    {
                                        case 5:
                                            missile = CreateProjectile(1030, Libraries.Magic3, true, 5, 30, 5);//normal arrow
                                            StanceTime = CMain.Time + StanceDelay;
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + 121 * 10 + 2);
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.VampireShot:
                                case Spell.PoisonShot:
                                case Spell.CrippleShot:
                                    MapObject ob = MapControl.GetObject(TargetID);
                                    Effect eff;
                                    int exFrameStart = 0;
                                    if (Spell == Spell.PoisonShot) exFrameStart = 200;
                                    if (Spell == Spell.CrippleShot) exFrameStart = 400;
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + ((Spell == Spell.CrippleShot) ? 136 : 121) * 10);//M136-0
                                            missile = CreateProjectile(1930 + exFrameStart, Libraries.Magic3, true, 5, 10, 5);
                                            StanceTime = CMain.Time + StanceDelay;
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    if (ob != null)
                                                    {
                                                        if (Spell == Spell.CrippleShot)
                                                        {
                                                            int exIdx = 0;
                                                            if (this == User)
                                                            {
                                                                //
                                                                if (GameScene.Scene.Buffs.Where(x => x.Type == BuffType.VampireShot).Any()) exIdx = 20;
                                                                if (GameScene.Scene.Buffs.Where(x => x.Type == BuffType.PoisonShot).Any()) exIdx = 10;
                                                            }
                                                            else
                                                            {
                                                                if (Buffs.Where(x => x == BuffType.VampireShot).Any()) exIdx = 20;
                                                                if (Buffs.Where(x => x == BuffType.PoisonShot).Any()) exIdx = 10;
                                                            }

                                                            //GameScene.Scene.ChatDialog.ReceiveChat("Debug: "+exIdx.ToString(),ChatType.System);

                                                            ob.Effects.Add(eff = new Effect(Libraries.Magic3, 2490 + exIdx, 7, 1000, ob));
                                                            SoundManager.PlaySound(20000 + 136 * 10 + 5 + (exIdx / 10));//sound M136-5/7

                                                            if (exIdx == 20)
                                                                eff.Complete += (o1, e1) =>
                                                                {
                                                                    SoundManager.PlaySound(20000 + 45 * 10 + 2);//sound M45-2
                                                                    Effects.Add(new Effect(Libraries.Magic3, 2100, 8, 1000, this));
                                                                };
                                                        }

                                                        if (Spell == Spell.VampireShot || Spell == Spell.PoisonShot)
                                                        {
                                                            ob.Effects.Add(eff = new Effect(Libraries.Magic3, 2090 + exFrameStart, 6, 1000, ob));
                                                            SoundManager.PlaySound(20000 + (133 + (exFrameStart / 100)) * 10 + 2);//sound M133-2 or M135-2
                                                            if (Spell == Spell.VampireShot)
                                                                eff.Complete += (o1, e1) =>
                                                                {
                                                                    SoundManager.PlaySound(20000 + 45 * 10 + 2);//sound M45-2
                                                                    Effects.Add(new Effect(Libraries.Magic3, 2100 + exFrameStart, 8, 1000, this));
                                                                };
                                                        }
                                                    }
                                                    //SoundManager.PlaySound(20000 + 121 * 10 + 2);//sound M121-2
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.NapalmShot:
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + 138 * 10);//M138-0
                                            missile = CreateProjectile(2530, Libraries.Magic3, true, 6, 50, 4);
                                            StanceTime = CMain.Time + StanceDelay;
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + 138 * 10 + 2);//M138-2
                                                    MapControl.Effects.Add(new Effect(Libraries.Magic3, 2690, 10, 1000, TargetPoint));
                                                };
                                            }
                                            break;
                                    }
                                    break;
                            }
                        }
                    }
                    if (WingEffect > 0 && CMain.Time >= NextMotion2)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;

                case MirAction.Struck:
                case MirAction.MountStruck:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }
                    if (WingEffect > 0 && CMain.Time >= NextMotion2)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;
                case MirAction.Spell:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            if (Cast)
                            {

                                MapObject ob = MapControl.GetObject(TargetID);

                                Missile missile;
                                Effect effect;
                                switch (Spell)
                                {
                                    #region FireBall

                                    case Spell.FireBall:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        missile = CreateProjectile(10, Libraries.Magic, true, 6, 30, 4);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic, 170, 10, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.FireBall * 10 + 2);
                                            };
                                        }
                                        break;

                                    #endregion

                                    #region GreatFireBall

                                    case Spell.GreatFireBall:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        missile = CreateProjectile(410, Libraries.Magic, true, 6, 30, 4);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic, 570, 10, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.GreatFireBall * 10 + 2);
                                            };
                                        }
                                        break;

                                    #endregion

                                    #region Healing

                                    case Spell.Healing:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 370, 10, 800, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic, 370, 10, 800, ob));
                                        break;

                                    #endregion

                                    #region ElectricShock

                                    case Spell.ElectricShock:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 1570, 10, 1000, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic, 1570, 10, 1000, ob));
                                        break;
                                    #endregion

                                    #region Poisoning

                                    case Spell.Poisoning:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        if (ob != null)
                                            ob.Effects.Add(new Effect(Libraries.Magic, 770, 10, 1000, ob));
                                        break;
                                    #endregion

                                    #region HellFire

                                    case Spell.HellFire:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);

                                        
                                        Point dest = CurrentLocation;
                                        for (int i = 0; i < 4; i++)
                                        {
                                            dest = Functions.PointMove(dest, Direction, 1);
                                            if (!GameScene.Scene.MapControl.ValidPoint(dest)) break;
                                            effect = new Effect(Libraries.Magic, 930, 6, 500, dest) { Rate = 0.7F };

                                            effect.SetStart(CMain.Time + i * 50);
                                            MapControl.Effects.Add(effect);
                                        }

                                        if (SpellLevel == 3)
                                        {
                                            MirDirection dir = (MirDirection)(((int)Direction + 1) % 8);
                                            dest = CurrentLocation;
                                            for (int r = 0; r < 4; r++)
                                            {
                                                dest = Functions.PointMove(dest, dir, 1);
                                                if (!GameScene.Scene.MapControl.ValidPoint(dest)) break;
                                                effect = new Effect(Libraries.Magic, 930, 6, 500, dest) { Rate = 0.7F };

                                                effect.SetStart(CMain.Time + r * 50);
                                                MapControl.Effects.Add(effect);
                                            }

                                            dir = (MirDirection)(((int)Direction - 1 + 8) % 8);
                                            dest = CurrentLocation;
                                            for (int r = 0; r < 4; r++)
                                            {
                                                dest = Functions.PointMove(dest, dir, 1);
                                                if (!GameScene.Scene.MapControl.ValidPoint(dest)) break;
                                                effect = new Effect(Libraries.Magic, 930, 6, 500, dest) { Rate = 0.7F };

                                                effect.SetStart(CMain.Time + r * 50);
                                                MapControl.Effects.Add(effect);
                                            }
                                        }
                                        break;

                                    #endregion

                                    #region ThunderBolt

                                    case Spell.ThunderBolt:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);

                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 10, 5, 400, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic2, 10, 5, 400, ob));
                                        break;

                                    #endregion

                                    #region SoulFireBall

                                    case Spell.SoulFireBall:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic, 1360, 10, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.SoulFireBall * 10 + 2);
                                            };
                                        }
                                        break;

                                    #endregion

                                    #region EnergyShield

                                    case Spell.EnergyShield:

                                        //Effects.Add(new Effect(Libraries.Magic2, 1880, 9, Frame.Count * FrameInterval, this));
                                        //SoundManager.PlaySound(20000 + (ushort)Spell * 9);
                                        break;

                                    #endregion

                                    #region FireBang

                                    case Spell.FireBang:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        MapControl.Effects.Add(new Effect(Libraries.Magic, 1660, 10, 1000, TargetPoint));
                                        break;

                                    #endregion

                                    #region MassHiding

                                    case Spell.MassHiding:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                        {
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 1540, 10, 800, TargetPoint));
                                            SoundManager.PlaySound(20000 + (ushort)Spell.MassHiding * 10 + 1);
                                        };
                                        break;

                                    #endregion

                                    #region SoulShield

                                    case Spell.SoulShield:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                        {
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 1320, 15, 1200, TargetPoint));
                                            SoundManager.PlaySound(20000 + (ushort)Spell.SoulShield * 10 + 1);
                                        };
                                        break;

                                    #endregion

                                    #region BlessedArmour

                                    case Spell.BlessedArmour:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                        {
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 1340, 15, 1200, TargetPoint));
                                            SoundManager.PlaySound(20000 + (ushort)Spell.BlessedArmour * 10 + 1);
                                        };
                                        break;

                                    #endregion

                                    #region FireWall

                                    case Spell.FireWall:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        break;

                                    #endregion

                                    #region MassHealing

                                    case Spell.MassHealing:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        MapControl.Effects.Add(new Effect(Libraries.Magic, 1800, 10, 1000, TargetPoint));
                                        break;

                                    #endregion

                                    #region IceStorm

                                    case Spell.IceStorm:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        MapControl.Effects.Add(new Effect(Libraries.Magic, 3850, 20, 1300, TargetPoint));
                                        break;

                                    #endregion

                                    #region TurnUndead

                                    case Spell.TurnUndead:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 3930, 15, 1000, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic, 3930, 15, 1000, ob));
                                        break;
                                    #endregion

                                    #region IceThrust

                                    case Spell.IceThrust:

                                        Point location = Functions.PointMove(CurrentLocation, Direction, 1);

                                        MapControl.Effects.Add(new Effect(Libraries.Magic2, 1790 + (int)Direction * 10, 10, 10 * FrameInterval, location));
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        break;

                                    #endregion

                                    #region Revelation

                                    case Spell.Revelation:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 3990, 10, 1000, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic, 3990, 10, 1000, ob));
                                        break;
                                    #endregion

                                    #region FlameDisruptor

                                    case Spell.FlameDisruptor:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);

                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 140, 10, 600, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic2, 140, 10, 600, ob));
                                        break;

                                    #endregion

                                    #region FrostCrunch

                                    case Spell.FrostCrunch:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        missile = CreateProjectile(410, Libraries.Magic2, true, 4, 30, 6);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic2, 570, 8, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.FrostCrunch * 10 + 2);
                                            };
                                        }
                                        break;

                                    #endregion

                                    #region Purification

                                    case Spell.Purification:
                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 620, 10, 800, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic2, 620, 10, 800, ob));
                                        break;

                                    #endregion

                                    #region Curse

                                    case Spell.Curse:
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                        {
                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 950, 24, 2000, TargetPoint));
                                            SoundManager.PlaySound(20000 + (ushort)Spell.Curse * 10);
                                        };
                                        break;

                                    #endregion

                                    #region Hallucination

                                    case Spell.Hallucination:
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 48, 7);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic2, 1110, 10, 1000, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            };
                                        }
                                        break;

                                    #endregion

                                    #region Lightning

                                    case Spell.Lightning:
                                        Effects.Add(new Effect(Libraries.Magic, 970 + (int)Direction * 20, 6, Frame.Count * FrameInterval, this));
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        break;

                                    #endregion

                                    #region Vampirism

                                    case Spell.Vampirism:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);

                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 1060, 20, 1000, TargetPoint));
                                        else
                                        {
                                            ob.Effects.Add(effect = new Effect(Libraries.Magic2, 1060, 20, 1000, ob));
                                            effect.Complete += (o, e) =>
                                            {
                                                SoundManager.PlaySound(20000 + (ushort)Spell.Vampirism * 10 + 2);
                                                Effects.Add(new Effect(Libraries.Magic2, 1090, 10, 500, this));
                                            };
                                        }
                                        break;

                                    #endregion

                                    #region PoisonCloud

                                    case Spell.PoisonCloud:
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                            {
                                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            };

                                        break;

                                    #endregion

                                    #region Blizzard

                                    case Spell.Blizzard:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        //BlizzardFreezeTime = CMain.Time + 3000;
                                        break;

                                    #endregion

                                    #region MeteorStrike

                                    case Spell.MeteorStrike:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);
                                        //BlizzardFreezeTime = CMain.Time + 3000;
                                        break;

                                    #endregion

                                    #region Reincarnation

                                    case Spell.Reincarnation:
                                        ReincarnationStopTime = 0;
                                        break;

                                    #endregion

                                    #region SummonHolyDeva

                                    case Spell.SummonHolyDeva:
                                        Effects.Add(new Effect(Libraries.Magic, 1500, 10, Frame.Count * FrameInterval, this));
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        break;

                                    #endregion

                                    #region UltimateEnhancer

                                    case Spell.UltimateEnhancer:
                                        if (ob != null && ob != User)
                                            ob.Effects.Add(new Effect(Libraries.Magic2, 160, 15, 1000, ob));
                                        break;

                                    #endregion

                                    #region Plague

                                    case Spell.Plague:
                                        //SoundManager.PlaySound(20000 + (ushort)Spell.SoulShield * 10);
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                        {
                                            MapControl.Effects.Add(new Effect(Libraries.Magic3, 110, 10, 1200, TargetPoint));
                                            SoundManager.PlaySound(20000 + (ushort)Spell.Plague * 10 + 3);
                                        };
                                        break;

                                    #endregion

                                    #region TrapHexagon

                                    case Spell.TrapHexagon:
                                        if (ob != null)
                                        SoundManager.PlaySound(20000 + (ushort)Spell.TrapHexagon * 10 + 1);
                                        break;

                                    #endregion

                                    #region Trap

                                    case Spell.Trap:
                                        if (ob != null)
                                            SoundManager.PlaySound(20000 + (ushort)Spell.Trap * 10 + 1);
                                        break;

                                    #endregion

                                    #region CrescentSlash

                                    case Spell.CrescentSlash:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);
                                        break;

                                    #endregion

                                    #region NapalmShot

                                    case Spell.NapalmShot:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        MapControl.Effects.Add(new Effect(Libraries.Magic3, 1660, 10, 1000, TargetPoint));
                                        break;

                                    #endregion
                                }


                                Cast = false;
                            }
                            //if (ActionFeed.Count == 0)
                            //    ActionFeed.Add(new QueuedAction { Action = MirAction.Stance, Direction = Direction, Location = CurrentLocation });

                            StanceTime = CMain.Time + StanceDelay;
                            FrameIndex = Frame.Count - 1;
                            SetAction();

                        }
                        else
                        {
                            NextMotion += FrameInterval;

                        }
                    }
                    if (WingEffect > 0 && CMain.Time >= NextMotion2)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;
                case MirAction.Die:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            ActionFeed.Clear();
                            ActionFeed.Add(new QueuedAction { Action = MirAction.Dead, Direction = Direction, Location = CurrentLocation });
                            SetAction();
                        }
                        else
                        {
                            if (FrameIndex == 1)
                                PlayDieSound();

                            NextMotion += FrameInterval;
                        }
                    }
                    if (WingEffect > 0 && CMain.Time >= NextMotion2)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;
                case MirAction.Dead:
                    break;
                case MirAction.Revive:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            ActionFeed.Clear();
                            ActionFeed.Add(new QueuedAction { Action = MirAction.Standing, Direction = Direction, Location = CurrentLocation });
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }
                    break;

            }

            if (this == User) return;

            if ((CurrentAction == MirAction.Standing || CurrentAction == MirAction.MountStanding || CurrentAction == MirAction.Stance || CurrentAction == MirAction.Stance2 || CurrentAction == MirAction.DashFail) && NextAction != null)
                SetAction();
            //if Revive and dead set action

        }
        public int UpdateFrame(bool skip = true)
        {
            if (Frame == null) return 0;
            if (Poison.HasFlag(PoisonType.Slow) && !skip)
            {
                SkipFrameUpdate++;
                if (SkipFrameUpdate == 2)
                    SkipFrameUpdate = 0;
                else
                    return FrameIndex;
            }
            if (Frame.Reverse) return Math.Abs(--FrameIndex);

            return ++FrameIndex;
        }

        public int UpdateFrame2()
        {
            if (Frame == null) return 0;

            if (Frame.Reverse) return Math.Abs(--EffectFrameIndex);

            return ++EffectFrameIndex;
        }


        private Missile CreateProjectile(int baseIndex, MLibrary library, bool blend, int count, int interval, int skip, int lightDistance = 6, Color? lightColour = null)
        {
            MapObject ob = MapControl.GetObject(TargetID);

            if (ob != null) TargetPoint = ob.CurrentLocation;

            int duration = Functions.MaxDistance(CurrentLocation, TargetPoint) * 50;


            Missile missile = new Missile(library, baseIndex, duration / interval, duration, this, TargetPoint)
            {
                Target = ob,
                Interval = interval,
                FrameCount = count,
                Blend = blend,
                Skip = skip,
                Light = lightDistance,
                LightColour = lightColour == null ? Color.White : (Color)lightColour
            };

            Effects.Add(missile);

            return missile;
        }

        //Rebuild
        public void PlayStepSound()
        {
            int x = CurrentLocation.X - CurrentLocation.X % 2;
            int y = CurrentLocation.Y - CurrentLocation.Y % 2;
            if (GameScene.Scene.MapControl.M2CellInfo[x, y].FrontIndex > 199) return; //prevents any move sounds on non mir2 maps atm
            if (GameScene.Scene.MapControl.M2CellInfo[x, y].MiddleIndex > 199) return; //prevents any move sounds on non mir2 maps atm
            if (GameScene.Scene.MapControl.M2CellInfo[x, y].BackIndex > 199) return; //prevents any move sounds on non mir2 maps atm

            int moveSound;

            if (GameScene.Scene.MapControl.M2CellInfo[x, y].BackIndex > 99 && GameScene.Scene.MapControl.M2CellInfo[x, y].BackIndex < 199) //shanda tiles
            {
                PlayShandaStepSound(x, y, out moveSound);
            }
            else //wemade tiles
            {
                PlayWemadeStepSound(x, y, out moveSound);
            }

            if (RidingMount) moveSound = SoundList.MountWalkL;

            if (CurrentAction == MirAction.Running) moveSound += 2;
            if (FrameIndex == 4) moveSound++;

            SoundManager.PlaySound(moveSound);
        }
        private void PlayWemadeStepSound(int x, int y, out int moveSound)
        {
            int index = (GameScene.Scene.MapControl.M2CellInfo[x, y].BackImage & 0x1FFFF) - 1;
            index = (GameScene.Scene.MapControl.M2CellInfo[x, y].FrontIndex - 2) * 10000 + index;

            if (index >= 0 && index <= 10000)
            {
                if ((index >= 330 && index <= 349) || (index >= 450 && index <= 454) || (index >= 550 && index <= 554) ||
                    (index >= 750 && index <= 754) || (index >= 950 && index <= 954) || (index >= 1250 && index <= 1254) ||
                    (index >= 1400 && index <= 1424) || (index >= 1455 && index <= 1474) || (index >= 1500 && index <= 1524) ||
                    (index >= 1550 && index <= 1574))
                    moveSound = SoundList.WalkLawnL;
                else if ((index >= 250 && index <= 254) || (index >= 1005 && index <= 1009) || (index >= 1050 && index <= 1054) ||
                    (index >= 1060 && index <= 1064) || (index >= 1450 && index <= 1454) || (index >= 1650 && index <= 1654))
                    moveSound = SoundList.WalkRoughL;
                else if ((index >= 605 && index <= 609) || (index >= 650 && index <= 654) || (index >= 660 && index <= 664) ||
                    (index >= 2000 && index <= 2049) || (index >= 3025 && index <= 3049) || (index >= 2400 && index <= 2424) ||
                    (index >= 4625 && index <= 4649) || (index >= 4675 && index <= 4678))
                    moveSound = SoundList.WalkStoneL;
                else if ((index >= 1825 && index <= 1924) || (index >= 2150 && index <= 2174) || (index >= 3075 && index <= 3099) ||
                    (index >= 3325 && index <= 3349) || (index >= 3375 && index <= 3399))
                    moveSound = SoundList.WalkCaveL;
                else if (index == 3230 || index == 3231 || index == 3246 || index == 3277 || (index >= 3780 && index <= 3799))
                    moveSound = SoundList.WalkWoodL;
                else if (index >= 3825 && index <= 4434)
                    switch (index % 25)
                    {
                        case 0:
                            moveSound = SoundList.WalkWoodL;
                            break;
                        default:
                            moveSound = SoundList.WalkGroundL;
                            break;
                    }
                else if ((index >= 2075 && index <= 2099) || (index >= 2125 && index <= 2149))
                    moveSound = SoundList.WalkRoomL;
                else if (index >= 1800 && index <= 1824)
                    moveSound = SoundList.WalkWaterL;
                else moveSound = SoundList.WalkGroundL;

                if ((index >= 825 && index <= 1349) && (index - 825) / 25 % 2 == 0) moveSound = SoundList.WalkStoneL;
                if ((index >= 1375 && index <= 1799) && (index - 1375) / 25 % 2 == 0) moveSound = SoundList.WalkCaveL;
                if (index == 1385 || index == 1386 || index == 1391 || index == 1392) moveSound = SoundList.WalkWoodL;

                index = (GameScene.Scene.MapControl.M2CellInfo[x, y].MiddleImage & 0x7FFF) - 1;
                if (index >= 0 && index <= 115)
                    moveSound = SoundList.WalkGroundL;
                else if (index >= 120 && index <= 124)
                    moveSound = SoundList.WalkLawnL;

                index = (GameScene.Scene.MapControl.M2CellInfo[x, y].FrontImage & 0x7FFF) - 1;
                if ((index >= 221 && index <= 289) || (index >= 583 && index <= 658) || (index >= 1183 && index <= 1206) ||
                    (index >= 7163 && index <= 7295) || (index >= 7404 && index <= 7414))
                    moveSound = SoundList.WalkStoneL;
                else if ((index >= 3125 && index <= 3267) || (index >= 3757 && index <= 3948) || (index >= 6030 && index <= 6999))
                    moveSound = SoundList.WalkWoodL;
                if (index >= 3316 && index <= 3589)
                    moveSound = SoundList.WalkRoomL;
            }
            else
                moveSound = SoundList.WalkGroundL;
        }


        private void PlayShandaStepSound(int x, int y, out int moveSound)
        {
            int index = (GameScene.Scene.MapControl.M2CellInfo[x, y].BackImage & 0x1FFFF) - 1;
            index = (GameScene.Scene.MapControl.M2CellInfo[x, y].BackIndex - 100) * 100000 + index;

            var tt = GameScene.Scene.MapControl.M2CellInfo[x, y];

            //CMain.SendDebugMessage(string.Format("BackImage : {0}. BackIndex : {1}. MiddleImage : {2}. MiddleIndex {3}", tt.BackImage, tt.BackIndex, tt.MiddleImage, tt.MiddleIndex));

            switch (GameScene.Scene.MapControl.M2CellInfo[x, y].BackIndex)
            {
                case 100:
                    {
                        moveSound = SoundList.WalkWaterL;
                    }
                    break;
                case 101:
                    {
                        //Tiles2
                        if ((index >= 0 && index <= 74) || (index >= 79 && index <= 83) || (index >= 94 && index <= 204) || (index >= 209 && index <= 213) ||
                            (index >= 209 && index <= 213) || (index >= 224 && index <= 249) || (index >= 255 && index <= 258) || (index >= 274 && index <= 329) ||
                            (index >= 350 && index <= 404) || (index >= 424 && index <= 449) || (index >= 455 && index <= 459) || (index >= 474 && index <= 504) ||
                            (index >= 509 && index <= 513) || (index >= 524 && index <= 549) || (index >= 555 && index <= 559) || (index >= 565 && index <= 573) ||
                            (index >= 594 && index <= 704) || (index >= 709 && index <= 713) || (index >= 724 && index <= 748) || (index >= 755 && index <= 759) ||
                            (index >= 774 && index <= 904) || (index >= 909 && index <= 923) || (index >= 974 && index <= 1004) || (index >= 1055 && index <= 1058) ||
                            (index >= 1064 && index <= 1204) || (index >= 1209 && index <= 1213) || (index >= 1300 && index <= 1373) || (index >= 1574 && index <= 1604) ||
                            (index >= 1609 && index <= 1649) || (index >= 1655 && index <= 1659) || (index >= 1674 && index <= 1704) || (index >= 1709 && index <= 1723) ||
                            (index >= 1755 && index <= 1758) || (index >= 1765 && index <= 1799) || (index >= 2050 && index <= 2523) || (index >= 2724 && index <= 2849) ||
                            (index >= 2950 && index <= 2999) || (index >= 3150 && index <= 3804) || (index >= 3809 && index <= 3849) || (index >= 3855 && index <= 3858) ||
                            (index >= 4475 && index <= 4794) || (index >= 5024 && index <= 5054) || (index >= 5059 && index <= 5299))
                            moveSound = SoundList.WalkGroundL;
                        else if ((index >= 205 && index <= 208) || (index >= 214 && index <= 223) || (index >= 264 && index <= 273) || (index >= 330 && index <= 349) ||
                            (index >= 405 && index <= 423) || (index >= 460 && index <= 474) || (index >= 505 && index <= 508) || (index >= 514 && index <= 523) ||
                            (index >= 560 && index <= 564) || (index >= 714 && index <= 723) || (index >= 764 && index <= 773) || (index >= 905 && index <= 908) ||
                            (index >= 955 && index <= 973) || (index >= 1009 && index <= 1023) || (index >= 1205 && index <= 1208) || (index >= 5055 && index <= 5058))
                            moveSound = SoundList.WalkLawnL;
                        else if ((index >= 250 && index <= 254) || (index >= 259 && index <= 263) || (index >= 450 && index <= 454) || (index >= 550 && index <= 554) ||
                            (index >= 574 && index <= 593) || (index >= 705 && index <= 708) || (index >= 749 && index <= 754) || (index >= 924 && index <= 954) ||
                            (index >= 1005 && index <= 1008) || (index >= 1024 && index <= 1054) || (index >= 1059 && index <= 1063) || (index >= 760 && index <= 763) ||
                            (index >= 1214 && index <= 1299) || (index >= 1374 && index <= 1573) || (index >= 1605 && index <= 1608) || (index >= 1650 && index <= 1654) ||
                            (index >= 1660 && index <= 1673) || (index >= 1705 && index <= 1708) || (index >= 1724 && index <= 1754) || (index >= 1759 && index <= 1764) ||
                            (index >= 2555 && index <= 2558) || (index >= 2605 && index <= 2608) || (index >= 2655 && index <= 2658) || (index >= 2705 && index <= 2708))
                            moveSound = SoundList.WalkRoughL;
                        else if ((index >= 75 && index <= 78) || (index >= 84 && index <= 93) || (index >= 2524 && index <= 2554) || (index >= 2560 && index <= 2604) ||
                            (index >= 2609 && index <= 2654) || (index >= 2659 && index <= 2704) || (index >= 2709 && index <= 2723) || (index >= 2850 && index <= 2949) ||
                            (index >= 3805 && index <= 3808) || (index >= 3850 && index <= 3854) || (index >= 3859 && index <= 3873) || (index >= 5300 && index <= 5323) ||
                            (index >= 6052 && index <= 6118))
                            moveSound = SoundList.WalkStoneL;
                        else if ((index >= 4795 && index <= 5023))
                            moveSound = SoundList.WalkCaveL;
                        else if ((index >= 1800 && index <= 2049) || (index >= 3000 && index <= 3149))
                            moveSound = SoundList.WalkWaterL;
                        else if ((index >= 5324 && index <= 6051) || (index >= 6119 && index <= 6296))
                            moveSound = SoundList.WalkRoomL;
                        else moveSound = SoundList.WalkWaterL;
                    }
                    break;
                case 102:
                    {
                        //Tiles3
                        if ((index >= 0 && index <= 299) || (index >= 400 && index <= 449) || (index >= 455 && index <= 522) || (index >= 528 && index <= 531) ||
                        (index >= 1553 && index <= 1554) || (index >= 1560 && index <= 1561) || (index >= 1565 && index <= 1566) || (index >= 1569 && index <= 1699) ||
                        (index >= 1805 && index <= 1809) || (index >= 1850 && index <= 1854) || (index >= 1860 && index <= 1864) || (index >= 1950 && index <= 1954) ||
                        (index >= 2000 && index <= 2204) || (index >= 2300 && index <= 2653))
                            moveSound = SoundList.WalkGroundL;
                        else if ((index >= 300 && index <= 399) || (index >= 450 && index <= 454) || (index >= 524 && index <= 527) || (index >= 1705 && index <= 1709) ||
                            (index >= 1715 && index <= 1799) || (index >= 2205 && index <= 2299))
                            moveSound = SoundList.WalkLawnL;
                        else if ((index >= 1700 && index <= 1704) || (index >= 1710 && index <= 1714))
                            moveSound = SoundList.WalkRoughL;
                        else if ((index >= 1800 && index <= 1804) || (index >= 1810 && index <= 1849) || (index >= 1855 && index <= 1859) || (index >= 1865 && index <= 1949) ||
                            (index >= 1955 && index <= 1999))
                            moveSound = SoundList.WalkStoneL;
                        else if ((index >= 1411 && index <= 1550) || (index >= 1555 && index <= 1557))
                            moveSound = SoundList.WalkWoodL;
                        else if ((index >= 532 && index <= 1410) || (index >= 1551 && index <= 1552) || (index >= 1558 && index <= 1559) || (index >= 1562 && index <= 1564) ||
                            (index >= 1567 && index <= 1568))
                            moveSound = SoundList.WalkWaterL;
                        else moveSound = SoundList.WalkWaterL;
                    }
                    break;
                case 103:
                    {
                        //Tiles4
                        if ((index >= 0000 && index <= 199) || (index >= 0205 && index <= 209) || (index >= 0250 && index <= 254) || (index >= 260 && index <= 1481) ||
                            (index >= 1495 && index <= 1549) || (index >= 1555 && index <= 1559) || (index >= 2500 && index <= 3349) || (index >= 3355 && index <= 3359) ||
                            (index >= 3365 && index <= 3399) || (index >= 3500 && index <= 3899) || (index >= 4193 && index <= 4254) || (index >= 4650 && index <= 4849) ||
                            (index >= 5000 && index <= 5149) || (index >= 5155 && index <= 5399) || (index >= 5405 && index <= 5409) || (index >= 5415 && index <= 5449) ||
                            (index >= 5550 && index <= 5554) || (index >= 5560 && index <= 5564) || (index >= 5605 && index <= 5609) || (index >= 5615 && index <= 5625) ||
                            (index >= 5680 && index <= 5749) || (index >= 7400 && index <= 7649) || (index >= 7655 && index <= 7749) || (index >= 7900 && index <= 8099) ||
                            (index >= 8166 && index <= 8299) || (index >= 8305 && index <= 8309) || (index >= 8350 && index <= 8354) || (index >= 8360 && index <= 8364) ||
                            (index >= 8400 && index <= 8499) || (index >= 8605 && index <= 8609) || (index >= 8615 && index <= 8619) || (index >= 8623 && index <= 8654) ||
                            (index >= 8660 && index <= 8754) || (index >= 8805 && index <= 8809) || (index >= 9050 && index <= 9299) || (index >= 9450 && index <= 9549))
                            moveSound = SoundList.WalkGroundL;
                        else if ((index >= 200 && index <= 204) || (index >= 210 && index <= 249) || (index >= 255 && index <= 259) || (index >= 1482 && index <= 1494) ||
                            (index >= 1560 && index <= 2499) || (index >= 4255 && index <= 4299) || (index >= 4305 && index <= 4349) || (index >= 5555 && index <= 5559) ||
                            (index >= 5565 && index <= 5604) || (index >= 5610 && index <= 5614) || (index >= 7650 && index <= 7654) || (index >= 7750 && index <= 7899) ||
                            (index >= 8100 && index <= 8165) || (index >= 8300 && index <= 8304) || (index >= 8310 && index <= 8349) || (index >= 8355 && index <= 8359) ||
                            (index >= 8365 && index <= 8399) || (index >= 8505 && index <= 8599) || (index >= 8610 && index <= 8614) || (index >= 8620 && index <= 8622) ||
                            (index >= 8655 && index <= 8659) || (index >= 8755 && index <= 8799) || (index >= 8810 && index <= 8849) || (index >= 8950 && index <= 8999) ||
                            (index >= 9005 && index <= 9049) || (index >= 9550 && index <= 9554))
                            moveSound = SoundList.WalkLawnL;
                        else if ((index >= 4300 && index <= 4304) || (index >= 8600 && index <= 8604) || (index >= 1550 && index <= 1554) || (index >= 4131 && index <= 4193) ||
                            (index >= 4350 && index <= 4549) || (index >= 5150 && index <= 5154) || (index >= 8500 && index <= 8504) || (index >= 8800 && index <= 8804) ||
                            (index >= 8850 && index <= 8949) || (index >= 9000 && index <= 9004))
                            moveSound = SoundList.WalkRoughL;
                        else if ((index >= 3350 && index <= 3354) || (index >= 3360 && index <= 3364) || (index >= 3400 && index <= 3499) || (index >= 5400 && index <= 5404) ||
                            (index >= 5410 && index <= 5414) || (index >= 5450 && index <= 5549) || (index >= 5626 && index <= 5679) || (index >= 9300 && index <= 9449) ||
                            (index >= 9555 && index <= 9749))
                            moveSound = SoundList.WalkStoneL;
                        else if ((index >= 3900 && index <= 4130) || (index >= 4550 && index <= 4649) || (index >= 4850 && index <= 4999) || (index >= 5750 && index <= 7399) ||
                            (index >= 9750 && index <= 11349))
                            moveSound = SoundList.WalkCaveL;
                        else if ((index >= 11350 && index <= 13534))
                            moveSound = SoundList.WalkWoodL;
                        else moveSound = SoundList.WalkWaterL;
                    }
                    break;
                case 104:
                    {
                        moveSound = SoundList.WalkWaterL;
                    }
                    break;
                case 105:
                    {
                        //Tiles6
                        if (index >= 0 && index <= 1539)
                            moveSound = SoundList.WalkLawnL;
                        else if (index >= 1540 && index <= 2368)
                            moveSound = SoundList.WalkRoomL;
                        else
                            moveSound = SoundList.WalkWaterL;
                    }
                    break;
                case 106:
                    {
                        //Tiles7
                        if ((index >= 0 && index <= 19) || (index >= 24 && index <= 053) || (index >= 56 && index <= 69) || (index >= 72 && index <= 74) ||
                            (index >= 77 && index <= 93) || (index >= 99 && index <= 115) || (index >= 121 && index <= 124) || (index >= 127 && index <= 132) ||
                            (index >= 135 && index <= 141) || (index >= 185 && index <= 190) || (index >= 191 && index <= 200) || (index >= 927 && index <= 940) ||
                            (index >= 961 && index <= 1160))
                            moveSound = SoundList.WalkGroundL;
                        else if ((index >= 201 && index <= 926) || (index >= 941 && index <= 960) || (index >= 1161 && index <= 3310))
                            moveSound = SoundList.WalkLawnL;
                        else if ((index >= 96 && index <= 98))
                            moveSound = SoundList.WalkWoodL;
                        else if ((index >= 94 && index <= 95) || (index >= 151 && index <= 184))
                            moveSound = SoundList.WalkStoneL;
                        else if ((index >= 20 && index <= 23) || (index >= 54 && index <= 55) || (index >= 70 && index <= 71) || (index >= 75 && index <= 76) ||
                            (index >= 116 && index <= 120))
                            moveSound = SoundList.WalkCaveL;
                        else if ((index >= 125 && index <= 126) || (index >= 133 && index <= 134) || (index >= 142 && index <= 151))
                            moveSound = SoundList.WalkWaterL;
                        else moveSound = SoundList.WalkWaterL;
                    }
                    break;
                case 107:
                    {
                        //Tiles8
                        if (index >= 0 && index <= 1215)
                            moveSound = SoundList.WalkCaveL;
                        else moveSound = SoundList.WalkWaterL;
                    }
                    break;
                default:
                    moveSound = SoundList.WalkWaterL;
                    break;
            }

            #region BackTiles
            //if (index >= 0 && index <= 99999)
            //else if (index >= 100000 && index <= 199999)
            //else if (index >= 200000 && index <= 299999)
            //else if (index >= 300000 && index <= 399999)
            //else if (index >= 400000 && index <= 499999)
            //else if (index >= 500000 && index <= 599999)           
            //else if (index >= 600000 && index <= 699999)          
            //else if (index >= 700000 && index <= 799999)            
            //else moveSound = SoundList.WalkWaterL;
            #endregion

            #region MiddleTiles
            index = (GameScene.Scene.MapControl.M2CellInfo[x, y].MiddleImage & 0x1FFFF) - 1;
            index = (GameScene.Scene.MapControl.M2CellInfo[x, y].MiddleIndex - 110) * 100000 + index;

            if (index >= 0 && index <= 99999)
            {
                //SMTiles
                if (index >= 0 && index <= 7)
                    moveSound = SoundList.WalkGroundL;
                else if (index >= 8 && index <= 1175)
                    moveSound = SoundList.WalkLawnL;
            }

            else if (index >= 100000 && index <= 199999)
            {
                //SMTiles2
                if (index >= 100000 && index <= 106205)
                    moveSound = SoundList.WalkGroundL;
                else if (index >= 106206 && index <= 109914)
                    moveSound = SoundList.WalkStoneL;
            }

            else if (index >= 200000 && index <= 299999)
            {
                //SMTiles3
                if ((index >= 9119 && index <= 9235) || (index >= 9296 && index <= 209355) || (index >= 9416 && index <= 209475) || (index >= 9536 && index <= 209835) ||
                    (index >= 210556 && index <= 210688) || (index >= 210916 && index <= 211035) || (index >= 219200 && index <= 220540) || (index >= 220788 && index <= 221194) ||
                    (index >= 229475 && index <= 230959) || (index >= 231378 && index <= 231436))
                    moveSound = SoundList.WalkGroundL;
                else if ((index >= 209236 && index <= 209295) || (index >= 209356 && index <= 209415) || (index >= 209476 && index <= 209535) || (index >= 209836 && index <= 210435) ||
                    (index >= 210689 && index <= 210915) || (index >= 223242 && index <= 225299) || (index >= 226252 && index <= 227305) || (index >= 228128 && index <= 228132) ||
                    (index >= 228187 && index <= 228192) || (index >= 228245 && index <= 228249) || (index >= 228275 && index <= 228280) || (index >= 228358 && index <= 228454) ||
                    (index >= 228974 && index <= 229474) || (index >= 230960 && index <= 231377))
                    moveSound = SoundList.WalkLawnL;
                else if ((index >= 300000 && index <= 209118) || (index >= 220541 && index <= 220787) || (index >= 221195 && index <= 223241) || (index >= 225300 && index <= 226251) ||
                    (index >= 227306 && index <= 228127) || (index >= 228133 && index <= 228186) || (index >= 228193 && index <= 228244) || (index >= 228250 && index <= 228274) ||
                    (index >= 228281 && index <= 228357) || (index >= 228455 && index <= 228973) || (index >= 231437 && index <= 231703))
                    moveSound = SoundList.WalkStoneL;
                else if ((index >= 211036 && index <= 219199))
                    moveSound = SoundList.WalkRoomL;
                else if ((index >= 210436 && index <= 210555))
                    moveSound = SoundList.WalkRoughL;
            }

            else if (index >= 300000 && index <= 399999)
            {
                //SMTiles4
                if ((index >= 300000 && index <= 300682) || (index >= 300695 && index <= 300699) || (index >= 300714 && index <= 300718) || (index >= 300733 && index <= 300745) ||
                    (index >= 300752 && index <= 300829) || (index >= 300833 && index <= 300849) || (index >= 300852 && index <= 300904) || (index >= 300907 && index <= 300920) ||
                    (index >= 300923 && index <= 300935) || (index >= 300939 && index <= 301088) || (index >= 301105 && index <= 301106) || (index >= 301112 && index <= 301113) ||
                    (index >= 301137 && index <= 301138) || (index >= 301422 && index <= 301423) || (index >= 301441 && index <= 301446) || (index >= 301460 && index <= 301467) ||
                    (index >= 301764 && index <= 301767) || (index >= 301772 && index <= 301786) || (index >= 301790 && index <= 302129) || (index >= 302132 && index <= 302291) ||
                    (index >= 302492 && index <= 302827) || (index >= 304419 && index <= 304646) || (index >= 306951 && index <= 306955) || (index >= 307027 && index <= 307104) ||
                    (index >= 307010 && index <= 307118) || (index >= 307124 && index <= 307133) || (index >= 307138 && index <= 307149) || (index >= 307162 && index <= 307167) ||
                    (index >= 307243 && index <= 308983) || (index >= 310892 && index <= 328054) || (index >= 304935 && index <= 305614) || (index >= 305619 && index <= 305627) ||
                    (index >= 305633 && index <= 305640) || (index >= 305647 && index <= 305651) || (index >= 305661 && index <= 305563) || (index >= 305675 && index <= 305676) ||
                    (index >= 305989 && index <= 306437) || (index >= 306457 && index <= 306474) || (index >= 306484 && index <= 306505) || (index >= 306512 && index <= 306533) ||
                    (index >= 306540 && index <= 306625) || (index >= 306636 && index <= 306761) || (index >= 306788 && index <= 306929) || (index >= 306936 && index <= 306942))
                    moveSound = SoundList.WalkGroundL;
                else if ((index >= 301468 && index <= 301515) || (index >= 302130 && index <= 302131) || (index >= 302292 && index <= 302491) || (index >= 302828 && index <= 303063))
                    moveSound = SoundList.WalkRoughL;
                else if ((index >= 301516 && index <= 301555) || (index >= 304647 && index <= 304934) || (index >= 305615 && index <= 305618) || (index >= 305628 && index <= 303632) ||
                    (index >= 305641 && index <= 305646) || (index >= 305652 && index <= 305660) || (index >= 305664 && index <= 305674) || (index >= 305677 && index <= 305988) ||
                    (index >= 306438 && index <= 306456) || (index >= 306475 && index <= 306483) || (index >= 306506 && index <= 306511) || (index >= 306534 && index <= 306539) ||
                    (index >= 306626 && index <= 306635) || (index >= 306762 && index <= 306787) || (index >= 306930 && index <= 306935) || (index >= 306943 && index <= 306950) ||
                    (index >= 306956 && index <= 307026) || (index >= 307105 && index <= 307109) || (index >= 307119 && index <= 307123) || (index >= 307134 && index <= 307137) ||
                    (index >= 307150 && index <= 307161) || (index >= 307168 && index <= 307242) || (index >= 308983 && index <= 310891))
                    moveSound = SoundList.WalkLawnL;
                else if ((index >= 300691 && index <= 300694) || (index >= 301155 && index <= 301164) || (index >= 301167 && index <= 301173) || (index >= 301181 && index <= 301185) ||
                    (index >= 301194 && index <= 301197) || (index >= 301208 && index <= 301211) || (index >= 301223 && index <= 301225) || (index >= 301233 && index <= 301235) ||
                    (index >= 301238 && index <= 301239) || (index >= 301247 && index <= 301250) || (index >= 301254 && index <= 301256) || (index >= 301262 && index <= 301265) ||
                    (index >= 301271 && index <= 301273) || (index >= 301277 && index <= 301280) || (index >= 301288 && index <= 301295) || (index >= 301305 && index <= 301310) ||
                    (index >= 301322 && index <= 301325) || (index >= 301339 && index <= 301341) || (index >= 301350 && index <= 301352) || (index >= 301356 && index <= 301358) ||
                    (index >= 301368 && index <= 301375) || (index >= 301386 && index <= 301392) || (index >= 301404 && index <= 301409) || (index >= 301424 && index <= 301427) ||
                    (index >= 301587 && index <= 301590) || (index >= 301605 && index <= 301608) || (index >= 301622 && index <= 301625) || (index >= 301638 && index <= 301642) ||
                    (index >= 301655 && index <= 301660) || (index >= 301666 && index <= 301678) || (index >= 301683 && index <= 301685) || (index >= 301688 && index <= 301692) ||
                    (index >= 301700 && index <= 301709) || (index >= 301718 && index <= 301728) || (index >= 301736 && index <= 301744) || (index >= 301754 && index <= 301763))
                    moveSound = SoundList.WalkWoodL;
                else if ((index >= 303064 && index <= 304418))
                    moveSound = SoundList.WalkRoomL;
                else if ((index >= 328055 && index <= 332912))
                    moveSound = SoundList.WalkCaveL;
                else if ((index >= 300683 && index <= 300690) || (index >= 300700 && index <= 300713) || (index >= 300719 && index <= 300732) || (index >= 300746 && index <= 300751) ||
                    (index >= 300830 && index <= 300832) || (index >= 300850 && index <= 300851) || (index >= 300905 && index <= 300906) || (index >= 300921 && index <= 300922) ||
                    (index >= 300936 && index <= 300938) || (index >= 301089 && index <= 301104) || (index >= 301107 && index <= 301111) || (index >= 301114 && index <= 301136) ||
                    (index >= 301139 && index <= 301154) || (index >= 301165 && index <= 301166) || (index >= 301174 && index <= 301180) || (index >= 301186 && index <= 301193) ||
                    (index >= 301198 && index <= 301207) || (index >= 301212 && index <= 301222) || (index >= 301226 && index <= 301232) || (index >= 301236 && index <= 301237) ||
                    (index >= 301240 && index <= 301246) || (index >= 301251 && index <= 301253) || (index >= 301258 && index <= 301261) || (index >= 301266 && index <= 301270) ||
                    (index >= 301274 && index <= 301276) || (index >= 301281 && index <= 301287) || (index >= 301296 && index <= 301304) || (index >= 301311 && index <= 301321) ||
                    (index >= 301326 && index <= 301338) || (index >= 301342 && index <= 301349) || (index >= 301354 && index <= 301355) || (index >= 301359 && index <= 301367) ||
                    (index >= 301376 && index <= 301385) || (index >= 301393 && index <= 301403) || (index >= 301410 && index <= 301421) || (index >= 301428 && index <= 301440) ||
                    (index >= 301447 && index <= 301459) || (index >= 301556 && index <= 301586) || (index >= 301591 && index <= 301604) || (index >= 301609 && index <= 301621) ||
                    (index >= 301626 && index <= 301637) || (index >= 301643 && index <= 301654) || (index >= 301661 && index <= 301665) || (index >= 301679 && index <= 301682) ||
                    (index >= 301686 && index <= 301687) || (index >= 301693 && index <= 301699) || (index >= 301710 && index <= 301717) || (index >= 301729 && index <= 301735) ||
                    (index >= 301768 && index <= 301771) || (index >= 301787 && index <= 301789) || (index >= 301745 && index <= 301753))
                    moveSound = SoundList.WalkWaterL;
            }

            else if (index >= 400000 && index <= 499999)
            {
                //SMTiles5 (114 image)
                if ((index >= 403165 && index <= 422976) || (index >= 423789 && index <= 424650))
                    moveSound = SoundList.WalkGroundL;
                else if ((index >= 402773 && index <= 403164) || (index >= 422977 && index <= 423788))
                    moveSound = SoundList.WalkLawnL;
                else if ((index >= 400000 && index <= 402772) || (index >= 431256 && index <= 431302))
                    moveSound = SoundList.WalkStoneL;
                else if ((index >= 424651 && index <= 430455))
                    moveSound = SoundList.WalkRoomL;
                else if ((index >= 430456 && index <= 431255))
                    moveSound = SoundList.WalkRoughL;
            }

            else if (index >= 500000 && index <= 599999)
            {
                //SMTiles6 (115 image)
                if ((index >= 500000 && index <= 501844) || (index >= 503294 && index <= 509086) || (index >= 509520 && index <= 512812) || (index >= 514854 && index <= 515035) ||
                    (index >= 521326 && index <= 522110) || (index >= 526820 && index <= 528871))
                    moveSound = SoundList.WalkGroundL;
                else if ((index >= 501845 && index <= 503293) || (index >= 513557 && index <= 513745))
                    moveSound = SoundList.WalkLawnL;
                else if ((index >= 509087 && index <= 509519) || (index >= 512813 && index <= 512909) || (index >= 516308 && index <= 521325) || (index >= 522111 && index <= 526819) ||
                    (index >= 528872 && index <= 530723))
                    moveSound = SoundList.WalkRoomL;
                else if ((index >= 512910 && index <= 513556) || (index >= 513746 && index <= 514853) || (index >= 515037 && index <= 516307))
                    moveSound = SoundList.WalkStoneL;
            }

            else if (index >= 600000 && index <= 699999)
            {
                //SMTiles7
            }

            else if (index >= 700000 && index <= 799999)
            {
                //SMTiles8
                if (index >= 700000 && index <= 701702)
                    moveSound = SoundList.WalkCaveL;

            }

            #endregion

        }


        public void PlayStruckSound()
        {
            if (RidingMount)
            {
                if (MountType < 7)
                    SoundManager.PlaySound(CMain.Random.Next(10179, 10181));
                else if (MountType < 12)
                    SoundManager.PlaySound(CMain.Random.Next(10193, 10194));

                return;
            }

            int add = 0;
            if (Class != MirClass.Assassin) //Archer to add?
                switch (Armour)
                {
                    case 3:
                    case 6:
                    case 9:
                        add = 10;
                        break;
                }

            switch (StruckWeapon)
            {
                case 0:
                case 23:
                case 1:
                case 12:
                case 28:
                case 40:
                    SoundManager.PlaySound(SoundList.StruckBodySword + add);
                    break;
                case 2:
                case 8:
                case 11:
                case 15:
                case 18:
                case 20:
                case 25:
                case 31:
                case 33:
                case 34:
                case 37:
                case 41:
                    SoundManager.PlaySound(SoundList.StruckBodySword + add);
                    break;
                case 3:
                case 5:
                case 7:
                case 9:
                case 13:
                case 19:
                case 24:
                case 26:
                case 29:
                case 32:
                case 35:
                    SoundManager.PlaySound(SoundList.StruckBodySword + add);
                    break;
                case 4:
                case 14:
                case 16:
                case 38:
                    SoundManager.PlaySound(SoundList.StruckBodyAxe + add);
                    break;
                case 6:
                case 10:
                case 17:
                case 22:
                case 27:
                case 30:
                case 36:
                case 39:
                    SoundManager.PlaySound(SoundList.StruckBodyLongStick + add);
                    break;
                case 21:
                    SoundManager.PlaySound(SoundList.StruckBodyLongStick + add);
                    break;
                case -1:
                    SoundManager.PlaySound(SoundList.StruckBodyFist + add);
                    break;
            }
        }
        public void PlayFlinchSound()
        {
            SoundManager.PlaySound(FlinchSound);
        }
        public void PlayAttackSound()
        {
            if (RidingMount)
            {
                if (MountType < 7)
                    SoundManager.PlaySound(CMain.Random.Next(10181, 10184));
                else if (MountType < 12)
                    SoundManager.PlaySound(CMain.Random.Next(10190, 10193));

                return;
            }

            if (Weapon >= 0 && Class == MirClass.Assassin)
            {
                SoundManager.PlaySound(SoundList.SwingShort);
                return;
            }

            if (Class == MirClass.Archer && HasClassWeapon)
            {
                return;
            }

            switch (Weapon)
            {
                case 0:
                case 23:
                case 28:
                case 40:
                    SoundManager.PlaySound(SoundList.SwingWood);
                    break;
                case 1:
                case 12:
                    SoundManager.PlaySound(SoundList.SwingShort);
                    break;
                case 2:
                case 8:
                case 11:
                case 15:
                case 18:
                case 20:
                case 25:
                case 31:
                case 33:
                case 34:
                case 37:
                case 41:
                    SoundManager.PlaySound(SoundList.SwingSword);
                    break;
                case 3:
                case 5:
                case 7:
                case 9:
                case 13:
                case 19:
                case 24:
                case 26:
                case 29:
                case 32:
                case 35:
                    SoundManager.PlaySound(SoundList.SwingSword2);
                    break;
                case 4:
                case 14:
                case 16:
                case 38:
                    SoundManager.PlaySound(SoundList.SwingAxe);
                    break;
                case 6:
                case 10:
                case 17:
                case 22:
                case 27:
                case 30:
                case 36:
                case 39:
                    SoundManager.PlaySound(SoundList.SwingLong);
                    break;
                case 21:
                    SoundManager.PlaySound(SoundList.SwingClub);
                    break;
                default:
                    SoundManager.PlaySound(SoundList.SwingFist);
                    break;
            }
        }

        public void PlayDieSound()
        {
            if (Gender == 0) { SoundManager.PlaySound(SoundList.MaleDie); }
            else { SoundManager.PlaySound(SoundList.FemaleDie); }
        }

        public void PlayMountSound()
        {
            if (RidingMount)
            {
                if(MountType < 7)
                    SoundManager.PlaySound(10218);
                else if (MountType < 12)
                    SoundManager.PlaySound(10188);
            }
            else
            {
                if (MountType < 7)
                    SoundManager.PlaySound(10219);
                else if (MountType < 12)
                    SoundManager.PlaySound(10189);
            }
        }


        public override void Draw()
        {
            DrawBehindEffects(Settings.Effect);

            float oldOpacity = DXManager.Opacity;
            if (Hidden && !DXManager.Blending) DXManager.SetOpacity(0.5F);

            DrawMount();

            if (!RidingMount)
            {
                if (Direction == MirDirection.Left || Direction == MirDirection.Up || Direction == MirDirection.UpLeft || Direction == MirDirection.DownLeft)
                    DrawWeapon();
                else
                    DrawWeapon2();
            }

            DrawBody();

            DrawHead();

            if (this != User)
            {
                DrawWings();
            }

            if (!RidingMount)
            {
                if (Direction == MirDirection.UpRight || Direction == MirDirection.Right || Direction == MirDirection.DownRight || Direction == MirDirection.Down)
                    DrawWeapon();
                else
                    DrawWeapon2();

                if (Class == MirClass.Archer && HasClassWeapon)
                    DrawWeapon2();
            }

            DXManager.SetOpacity(oldOpacity);

            //if (Settings.Effect && this != User)
            //{
            //    DrawEffects();  
            //}
        }

        public override void DrawBehindEffects(bool effectsEnabled)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (!Effects[i].DrawBehind) continue;
                if (!Settings.LevelEffect && (Effects[i] is SpecialEffect) && ((SpecialEffect)Effects[i]).EffectType == 1) continue;
                if ((!effectsEnabled) && (!IsVitalEffect(Effects[i]))) continue;
                Effects[i].Draw();
            }
        }

        public override void DrawEffects(bool effectsEnabled)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (Effects[i].DrawBehind) continue;
                if (!Settings.LevelEffect && (Effects[i] is SpecialEffect) && ((SpecialEffect)Effects[i]).EffectType == 1) continue;
                if ((!effectsEnabled) && (!IsVitalEffect(Effects[i]))) continue;
                Effects[i].Draw();
            }

            if (!effectsEnabled) return;

            switch (CurrentAction)
            {
                case MirAction.Attack1:
                    switch (Spell)
                    {
                        case Spell.Slaying:
                            Libraries.Magic.DrawBlend(1820 + ((int)Direction * 10) + SpellLevel * 90 + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.DoubleSlash:
                            Libraries.Magic2.DrawBlend(1960 + ((int)Direction * 10) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.Thrusting:
                            Libraries.Magic.DrawBlend(2190 + ((int)Direction * 10) + SpellLevel * 90 + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.HalfMoon:
                            Libraries.Magic.DrawBlend(2560 + ((int)Direction * 10) + SpellLevel * 90 + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.TwinDrakeBlade:
                            Libraries.Magic2.DrawBlend(220 + ((int)Direction * 20) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.CrossHalfMoon:
                            Libraries.Magic2.DrawBlend(40 + ((int)Direction * 10) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.FlamingSword:
                            Libraries.Magic.DrawBlend(3480 + ((int)Direction * 10) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                    }
                    break;
                case MirAction.Attack4:

                    switch (Spell)
                    {
                        case Spell.DoubleSlash:
                            Libraries.Magic2.DrawBlend(2050 + ((int)Direction * 10) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.TwinDrakeBlade:
                            Libraries.Magic2.DrawBlend(226 + ((int)Direction * 20) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.FlamingSword:
                            Libraries.Magic.DrawBlend(3480 + ((int)Direction * 10) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                    }
                    break;
            }


        }

        public void DrawCurrentEffects()
        {
            if (CurrentEffect == SpellEffect.MagicShieldUp && !MagicShield)
            {
                MagicShield = true;
                Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, this) { Repeat = true });
                CurrentEffect = SpellEffect.None;
            }

            if (CurrentEffect == SpellEffect.ElementalBarrierUp && !ElementalBarrier)
            {
                ElementalBarrier = true;
                Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 16, 3200, this) { Repeat = true });
                CurrentEffect = SpellEffect.None;
            }

            if (ElementEffect > 0 && !HasElements)
            {
                HasElements = true;
                if (ElementEffect == 4)
                    Effects.Add(new ElementsEffect(Libraries.Magic3, 1660, 10, 10 * 100, this, true, 4, ElementOrbMax));
                else
                {
                    if (ElementEffect >= 1)
                        Effects.Add(new ElementsEffect(Libraries.Magic3, 1630, 10, 10 * 100, this, true, 1, ElementOrbMax));
                    if (ElementEffect >= 2)
                        Effects.Add(new ElementsEffect(Libraries.Magic3, 1640, 10, 10 * 100, this, true, 2, ElementOrbMax));
                    if (ElementEffect == 3)
                        Effects.Add(new ElementsEffect(Libraries.Magic3, 1650, 10, 10 * 100, this, true, 3, ElementOrbMax));
                }
                ElementEffect = 0;
            }
        }

        public override void DrawBlend()
        {
            DXManager.SetBlend(true, 0.3F);
            Draw();
            DXManager.SetBlend(false);
        }
        public void DrawBody()
        {
            if (BodyLibrary != null)
                BodyLibrary.Draw(DrawFrame + ArmourOffSet, DrawLocation, DrawColour, true);

            //BodyLibrary.DrawTinted(DrawFrame + ArmourOffSet, DrawLocation, DrawColour, Color.DarkSeaGreen);
        }
        public void DrawHead()
        {
            if (HairLibrary != null)
                HairLibrary.Draw(DrawFrame + HairOffSet, DrawLocation, DrawColour, true);
        }
        public void DrawWeapon()
        {
            if (Weapon < 0) return;

            if (WeaponLibrary1 != null)
                WeaponLibrary1.Draw(DrawFrame + WeaponOffSet, DrawLocation, DrawColour, true);
        }
        public void DrawWeapon2()
        {
            if (Weapon == -1) return;

            if (WeaponLibrary2 != null)
                WeaponLibrary2.Draw(DrawFrame + WeaponOffSet, DrawLocation, DrawColour, true);
        }
        public void DrawWings()
        {
            if (WingEffect <= 0 || WingEffect >= 100) return;

            if (WingLibrary != null)
                WingLibrary.DrawBlend(DrawWingFrame + WingOffset, DrawLocation, DrawColour, true);
        }


        public void DrawMount()
        {
            if (MountType < 0 || !RidingMount) return;

            if (MountLibrary != null)
                MountLibrary.Draw(DrawFrame - 416 + MountOffset, DrawLocation, DrawColour, true);
        }

        private bool IsVitalEffect(Effect effect)
        {
            if ((effect.Library == Libraries.Magic) && (effect.BaseIndex == 3890))
                return true;
            if ((effect.Library == Libraries.Magic3) && (effect.BaseIndex == 1890))
                return true;
            return false;
        }

        public void GetBackStepDistance(int magicLevel)
        {
            JumpDistance = 0;
            if (InTrapRock) return;

            int travel = 0;
            bool blocked = false;
            int dist = (magicLevel == 0) ? 1 : magicLevel;//3 max
            MirDirection jumpDir = Functions.ReverseDirection(Direction);

            Point location = CurrentLocation;
            for (int i = 0; i < dist; i++)//step 1t/m3
            {
                location = Functions.PointMove(location, jumpDir, 1);
                if (!GameScene.Scene.MapControl.ValidPoint(location)) break;

                CellInfo cInfo = GameScene.Scene.MapControl.M2CellInfo[location.X, location.Y];
                if (cInfo.CellObjects != null)
                    for (int c = 0; c < cInfo.CellObjects.Count; c++)
                    {
                        MapObject ob = cInfo.CellObjects[c];
                        if (!ob.Blocking) continue;
                        blocked = true;
                        if ((cInfo.CellObjects == null) || blocked) break;
                    }
                if (blocked) break;
                travel++;
            }
            JumpDistance = travel;
        }

        public void GetFlashDashDistance(int magicLevel)
        {
            JumpDistance = 0;
            if (InTrapRock) return;

            int travel = 0;
            bool blocked = false;
            int dist = (magicLevel <= 1) ? 0 : 1;
            MirDirection jumpDir = Direction;

            Point location = CurrentLocation;
            for (int i = 0; i < dist; i++)
            {
                location = Functions.PointMove(location, jumpDir, 1);
                if (!GameScene.Scene.MapControl.ValidPoint(location)) break;

                CellInfo cInfo = GameScene.Scene.MapControl.M2CellInfo[location.X, location.Y];
                if (cInfo.CellObjects != null)
                    for (int c = 0; c < cInfo.CellObjects.Count; c++)
                    {
                        MapObject ob = cInfo.CellObjects[c];
                        if (!ob.Blocking) continue;
                        blocked = true;
                        if ((cInfo.CellObjects == null) || blocked) break;
                    }
                if (blocked) break;
                travel++;
            }
            JumpDistance = travel;
        }

        public bool IsDashAttack()
        {
            Point location = CurrentLocation;
            location = Functions.PointMove(location, Direction, 1);

            if (!GameScene.Scene.MapControl.ValidPoint(location)) return false;

            CellInfo cInfo = GameScene.Scene.MapControl.M2CellInfo[location.X, location.Y];

            if (cInfo.CellObjects != null)
            {
                for (int c = 0; c < cInfo.CellObjects.Count; c++)
                {
                    MapObject ob = cInfo.CellObjects[c];
                    if (ob == this) return false;
                    switch (ob.Race)
                    {
                        case ObjectType.Monster:
                        case ObjectType.Player:
                            return true;
                    }
                }
            }

            return false;
        }

        public override bool MouseOver(Point p)
        {
            return MapControl.MapLocation == CurrentLocation || BodyLibrary != null && BodyLibrary.VisiblePixel(DrawFrame + ArmourOffSet, p.Subtract(FinalDrawLocation), false);
        }

        public override void CreateLabel()
        {
            NameLabel = null;
            GuildLabel = null;

            for (int i = 0; i < LabelList.Count; i++)
            {
                if (LabelList[i].Text != Name || LabelList[i].ForeColour != NameColour) continue;
                NameLabel = LabelList[i];
                break;
            }

            for (int i = 0; i < LabelList.Count; i++)
            {
                if (LabelList[i].Text != GuildName || LabelList[i].ForeColour != NameColour) continue;
                GuildLabel = LabelList[i];
                break;
            }

            if (NameLabel != null && !NameLabel.IsDisposed && GuildLabel != null && !GuildLabel.IsDisposed) return;

            NameLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = Color.Transparent,
                ForeColour = NameColour,
                OutLine = true,
                OutLineColour = Color.Black,
                Text = Name,
            };
            NameLabel.Disposing += (o, e) => LabelList.Remove(NameLabel);
            LabelList.Add(NameLabel);
            
            GuildLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = Color.Transparent,
                ForeColour = NameColour,
                OutLine = true,
                OutLineColour = Color.Black,
                Text = GuildName,
            };
            GuildLabel.Disposing += (o, e) => LabelList.Remove(GuildLabel);
            LabelList.Add(GuildLabel);
        }

        public override void DrawName()
        {
            CreateLabel();

            if (NameLabel == null || GuildLabel == null) return;

            if (GuildName != "")
            {
                GuildLabel.Text = GuildName;
                GuildLabel.Location = new Point(DisplayRectangle.X + (50 - GuildLabel.Size.Width) / 2, DisplayRectangle.Y - (42 - GuildLabel.Size.Height / 2) + (Dead ? 35 : 8)); //was 48 -
                GuildLabel.Draw();
            }

            NameLabel.Text = Name;
            NameLabel.Location = new Point(DisplayRectangle.X + (50 - NameLabel.Size.Width) / 2, DisplayRectangle.Y - (32 - NameLabel.Size.Height / 2) + (Dead ? 35 : 8)); //was 48 -
            NameLabel.Draw();
        }

    }


    public class QueuedAction
    {
        public MirAction Action;
        public Point Location;
        public MirDirection Direction;
        public List<object> Params;
    }

}
