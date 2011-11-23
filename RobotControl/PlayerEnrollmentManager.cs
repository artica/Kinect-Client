using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Research.Kinect.Nui;

namespace KinectTest
{
    //--------------------------------------------------------------------------------------
    // States in the enrollment state machine
    //--------------------------------------------------------------------------------------
    public enum PlayerEnrollmentState
    {
        NoSkeleton = 0,
        NotInPosition,
        WaitingForHandRaise,
        Enrolled,
        LostEnrollment,
        
        NUM
    }

    //--------------------------------------------------------------------------------------
    // Enum for describing the player's location
    //--------------------------------------------------------------------------------------
    public enum OutOfPositionState
    {
        NotCentered,
        TooClose,
        TooFar,
        Centered
    }

    //--------------------------------------------------------------------------------------
    // Data structure used by the Enrollment Manager
    //--------------------------------------------------------------------------------------
    public struct EnrolledPlayer
    {
        public int SkeletonIndex;
        public PlayerEnrollmentState State;
        public OutOfPositionState PositionState;
    }
    //--------------------------------------------------------------------------------------
    // Class that handles enrolling players into the game, making them go through a process
    // of walking up to the camera, standing within a certain range, then raising their hand
    // above their head. Can handle up to two different players
    //--------------------------------------------------------------------------------------
    public class PlayerEnrollmentManager
    {


        //--------------------------------------------------------------------------------------
        // ctor: Defaults number of players to 1
        //--------------------------------------------------------------------------------------
        public PlayerEnrollmentManager()
        {
            ClearAllEnrollment();
            NumPlayers = 1;
        }

        //--------------------------------------------------------------------------------------
        // Resets player slots
        //--------------------------------------------------------------------------------------
        public void ClearAllEnrollment()
        {
            for (int I = 0; I < NumPlayers; ++I)
            {
                Players[I].State = PlayerEnrollmentState.NoSkeleton;
                Players[I].SkeletonIndex = -1;
            }
        }

        //--------------------------------------------------------------------------------------
        // Returns the skeleton of the specified player (if available)
        //--------------------------------------------------------------------------------------
        public SkeletonData GetPlayerSkeleton(int PlayerIndex)
        {
            if (Players[PlayerIndex].State == PlayerEnrollmentState.Enrolled)
                return Frame.Skeletons[Players[PlayerIndex].SkeletonIndex];
            else
                return null;
        }

        //--------------------------------------------------------------------------------------
        // Runs the enrollment state machine, called every frame
        //--------------------------------------------------------------------------------------
        public void Update(GameTime Time)
        {
            if (Frame == null)
                return;

            TimeSinceLastFrame += Time.ElapsedGameTime;

            if (TimeSinceLastFrame.TotalSeconds > 3) //Been over a few seconds since we've recieved a valid skeleton frame clear alll
            {
                frame = null;
                ClearAllEnrollment();
                return;
            }

            for (int I = 0; I < NumPlayers; ++I)
            {
                if (Players[I].SkeletonIndex == -1 || Frame.Skeletons[Players[I].SkeletonIndex].TrackingState == SkeletonTrackingState.Tracked)
                {
                    switch (Players[I].State)
                    {
                        case PlayerEnrollmentState.NoSkeleton:
                            FindSkeletonToEnroll(I);
                            break;

                        case PlayerEnrollmentState.NotInPosition:
                            CheckForCenteredSkeleton(I);
                            break;

                        case PlayerEnrollmentState.WaitingForHandRaise:
                            CheckForHandRaise(I);
                            break;

                        case PlayerEnrollmentState.Enrolled:
                            break;

                        default:
                            break;
                    }
                }
                else
                {
                    Players[I].SkeletonIndex = -1;
                    Players[I].State = PlayerEnrollmentState.NoSkeleton;
                }
            }
        }

        //--------------------------------------------------------------------------------------
        // Helper function for enrollment state machine. Iterates through the available
        // skeletons, and if one is available and not currently enrolled, starts the enrollment
        // process with that skeleton
        //--------------------------------------------------------------------------------------
        public void FindSkeletonToEnroll(int PlayerIndex)
        {
            bool SkeletonExists = false;
            int Index = -1;
            for(int I = 0; I < Frame.Skeletons.Length && !SkeletonExists; ++I)
            {
                if(Frame.Skeletons[I].TrackingState == SkeletonTrackingState.Tracked)
                {
                    bool AlreadyExists = false;
                    for (int J = 0; J < NumPlayers; ++J)
                    {
                        if (PlayerIndex != J && Players[J].SkeletonIndex == I) //Make sure we're not stealing another skeleton
                        {
                            AlreadyExists = true;
                        }
                    }
                    if (!AlreadyExists)
                    {
                        Index = I;
                        SkeletonExists = true;
                        break;
                    }
                    
                }
            }

            if(SkeletonExists)
            {
                Players[PlayerIndex].SkeletonIndex = Index;
                if (SkeletonInView(PlayerIndex))
                {
                    Players[PlayerIndex].State = PlayerEnrollmentState.WaitingForHandRaise;
                }
                else
                {
                    Players[PlayerIndex].State = PlayerEnrollmentState.NotInPosition;
                }

            }

        }

        //--------------------------------------------------------------------------------------
        // Helper function for enrollment state machine, checks to see if either hand has been
        // raised above the player's hand
        //--------------------------------------------------------------------------------------
        public void CheckForHandRaise(int PlayerIndex)
        {
            if (SkeletonInView(PlayerIndex))
            {
                if (HandRaised(Frame.Skeletons[Players[PlayerIndex].SkeletonIndex]))
                {
                    Players[PlayerIndex].State = PlayerEnrollmentState.Enrolled;
                }
            }
            else
            {
                Players[PlayerIndex].State = PlayerEnrollmentState.NotInPosition;
            }
        }

        //--------------------------------------------------------------------------------------
        // Simple function for checking that a skeleton's hand is above its head
        //--------------------------------------------------------------------------------------
        public bool HandRaised(SkeletonData Data)
        {
            Vector HeadPos = Data.Joints[JointID.Head].Position;
            Vector RightHand = Data.Joints[JointID.HandRight].Position;
            Vector LeftHand = Data.Joints[JointID.HandLeft].Position;

            return RightHand.Y > HeadPos.Y || LeftHand.Y > HeadPos.Y;
        }

        //--------------------------------------------------------------------------------------
        // Helper function for the enrollment state machine, used to determine if a skeleton is
        // inside the desired play area
        //--------------------------------------------------------------------------------------
        public void CheckForCenteredSkeleton(int PlayerIndex)
        {
            if (SkeletonInView(PlayerIndex))
            {
                Players[PlayerIndex].State = PlayerEnrollmentState.WaitingForHandRaise;
            }
        }

        //--------------------------------------------------------------------------------------
        // Function that classifies the position of a skeleton as too far, too close, not 
        // centered, or acceptable for play.
        //--------------------------------------------------------------------------------------
        public bool SkeletonInView(int PlayerIndex)
        {
            Vector Position = Frame.Skeletons[Players[PlayerIndex].SkeletonIndex].Position;

            if (Position.X < -.5f || Position.X > .5f)
            {
                Players[PlayerIndex].PositionState = OutOfPositionState.NotCentered;
            }

            else if (Position.Z < 2)
            {
                Players[PlayerIndex].PositionState = OutOfPositionState.TooClose;
            }
            else if (Position.Z > 3)
            {
                Players[PlayerIndex].PositionState = OutOfPositionState.TooFar;
            }
            else
            {
                Players[PlayerIndex].PositionState = OutOfPositionState.Centered;
                return true;
            }
            return false;
        }

        public EnrolledPlayer[] Players = new EnrolledPlayer[MAXPLAYERS];
        public TimeSpan TimeSinceLastFrame;

        private SkeletonFrame frame;
        public SkeletonFrame Frame
        {
            get
            {
                return frame;
            }
            set
            {
                frame = value;
                TimeSinceLastFrame = TimeSpan.Zero;
            }
        }

        const int MAXPLAYERS = 2;

        private int numPlayers;
        public int NumPlayers
        {
            get
            {
                return numPlayers;
            }
            set
            {
                if (value > 0 && value <= MAXPLAYERS)
                    numPlayers = value;
            }
        }
    }
}
