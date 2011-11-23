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
//using Coding4Fun.Kinect.Wpf;
using Microsoft.Research.Kinect.Nui;
using System.Diagnostics;
using Microsoft.Xna.Framework.Net;
using System.Net.Sockets;
using KinectTest;


namespace RobotControl
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        SpriteFont font;
        Runtime nui;
        System.Net.Sockets.TcpClient tcpClient;
        NetworkStream stream;

        PlayerEnrollmentManager EnrollmentManager = new PlayerEnrollmentManager();

        enum Direction
        {
            Neutral,
            Forward,
            Back,
            Left,
            Right,
            Count,
        };

        Direction direction = Direction.Neutral;
        Direction lastDirection = Direction.Count;

        float deltaX;
        float deltaZ;

        // We want to control how depth data gets converted into false-color data
        // for more intuitive visualization, so we keep 32-bit color frame buffer versions of
        // these, to be updated whenever we receive and process a 16-bit frame.
        const int RED_IDX = 2;
        const int GREEN_IDX = 1;
        const int BLUE_IDX = 0;
        byte[] depthFrame32 = new byte[320 * 240 * 4];

        // Textures
        Texture2D depth;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            nui = new Runtime();

            try
            {
                nui.Initialize(RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking | RuntimeOptions.UseColor);
            }
            catch (InvalidOperationException)
            {
                Debug.WriteLine("Runtime initialization failed. Please make sure Kinect device is plugged in.");
                return;
            }

            try
            {
                nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                nui.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);
            }
            catch (InvalidOperationException)
            {
                Debug.WriteLine("Failed to open stream. Please make sure to specify a supported image type and resolution.");
                return;
            }

            nui.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady);
            nui.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);
            nui.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_ColorFrameReady);

            try
            {
                tcpClient = new System.Net.Sockets.TcpClient("176.78.8.126", 6969);
                stream = tcpClient.GetStream();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            EnrollmentManager.NumPlayers = 1;

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {

            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            depth = new Texture2D(graphics.GraphicsDevice, 320, 240);
            font = Content.Load<SpriteFont>("gamefont");
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            stream.Close();
        }

        #region NUI Events
        void nui_DepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            PlanarImage Image = e.ImageFrame.Image;
            byte[] convertedDepthFrame = convertDepthFrame(Image.Bits);

            lock (depthFrame32)
            {
                depth.SetData(convertedDepthFrame);
            }

        }

        int activeSkeleton;
        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            //int trackedSkeletonCount = 0;

            EnrollmentManager.Frame = e.SkeletonFrame;

            /*foreach (SkeletonData sd in e.SkeletonFrame.Skeletons)
            {
                // the first found/tracked skeleton moves the mouse cursor
                if (sd.TrackingState == SkeletonTrackingState.Tracked)
                {
                    activeSkeleton = trackedSkeletonCount;
                    ++trackedSkeletonCount;

                    // make sure both hands are tracked
                    if ((sd.Joints[JointID.HandLeft].TrackingState == JointTrackingState.Tracked &&
                        sd.Joints[JointID.ShoulderRight].TrackingState == JointTrackingState.Tracked) &&
                        (sd.Joints[JointID.HandRight].TrackingState == JointTrackingState.Tracked &&
                        sd.Joints[JointID.ShoulderLeft].TrackingState == JointTrackingState.Tracked))
                    {
                        // get the left and right hand Joints
                        Joint jointRight = sd.Joints[JointID.HandRight];
                        Joint jointLeft = sd.Joints[JointID.HandLeft];

                        deltaZ = 0.0f;
                        deltaX = 0.0f;

                        // Decide between left / right
                        if (jointRight.Position.Y > jointLeft.Position.Y)
                        {
                            // Using Right Hand
                            Joint refRight = sd.Joints[JointID.ShoulderRight];
                            deltaZ = jointRight.Position.Z - refRight.Position.Z;
                            deltaX = jointRight.Position.X - refRight.Position.X;

                        }
                        else
                        {
                            // Using Left Hand
                            Joint refLeft = sd.Joints[JointID.ShoulderLeft];
                            deltaZ = jointLeft.Position.Z - refLeft.Position.Z;
                            deltaX = jointLeft.Position.X - refLeft.Position.X;
                        }

                        //lastDirection = direction;
                        direction = Direction.Neutral;

                        if (deltaX < -.3f)
                        {
                            // Move left
                            direction = Direction.Left;

                        }
                        else if (deltaX > .3f)
                        {
                            // Move right
                            direction = Direction.Right;
                        }
                        else if (deltaZ > -.2f)
                        {
                            // Move back
                            direction = Direction.Back;
                        }
                        else if (deltaZ < -.4f)
                        {
                            // Move forward
                            direction = Direction.Forward;
                        }

                        return;
                    }
                }

                if (trackedSkeletonCount > 0)
                    break;
            }
            if (trackedSkeletonCount == 0)
            {
                direction = Direction.Neutral;
            }*/
        }


        void nui_ColorFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
        }
        #endregion

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            if (!tcpClient.Connected)
                return;

            EnrollmentManager.Update(gameTime);

            SkeletonData skeleton = EnrollmentManager.GetPlayerSkeleton(0); // Only one player
            if (skeleton == null)
                return;

            // get the left and right hand Joints
            Joint jointRight = skeleton.Joints[JointID.HandRight];
            Joint jointLeft = skeleton.Joints[JointID.HandLeft];

            deltaZ = 0.0f;
            deltaX = 0.0f;

            // Decide between left / right
            if (jointRight.Position.Y > jointLeft.Position.Y)
            {
                // Using Right Hand
                Joint refRight = skeleton.Joints[JointID.ShoulderRight];
                deltaZ = jointRight.Position.Z - refRight.Position.Z;
                deltaX = jointRight.Position.X - refRight.Position.X;

            }
            else
            {
                // Using Left Hand
                Joint refLeft = skeleton.Joints[JointID.ShoulderLeft];
                deltaZ = jointLeft.Position.Z - refLeft.Position.Z;
                deltaX = jointLeft.Position.X - refLeft.Position.X;
            }

            //lastDirection = direction;
            direction = Direction.Neutral;

            if (deltaX < -.3f)
            {
                // Move left
                direction = Direction.Left;

            }
            else if (deltaX > .3f)
            {
                // Move right
                direction = Direction.Right;
            }
            else if (deltaZ > -.2f)
            {
                // Move back
                direction = Direction.Back;
            }
            else if (deltaZ < -.4f)
            {
                // Move forward
                direction = Direction.Forward;
            }


            if (direction != lastDirection)
            {
                lastDirection = direction;

                string data = "";
                switch (direction)
                {
                    case Direction.Neutral:
                        data = "p";
                        break;
                    case Direction.Left:
                        data = "a";
                        break;
                    case Direction.Right:
                        data = "d";
                        break;
                    case Direction.Forward:
                        data = "w";
                        break;
                    case Direction.Back:
                        data = "s";
                        break;
                }

                data += "[/TCP]";
                var enc = new System.Text.ASCIIEncoding();

                stream.Write(enc.GetBytes(data), 0, data.Length);
            }

            base.Update(gameTime);
        }

        static readonly Vector2 textPosX = new Vector2(10, 10);
        static readonly Vector2 textPosZ = new Vector2(10, 50);
        static readonly Vector2 textPosStatus = new Vector2(10, 90);
        static readonly Vector2 InstructionTextPos = new Vector2(10, 200);

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            Viewport viewport = graphics.GraphicsDevice.Viewport;
            Rectangle fullscreen = new Rectangle(0, 0, viewport.Width, viewport.Height);
            string statusString = "Neutral";

            switch (direction)
            {
                case Direction.Neutral:
                    statusString = "Neutral";
                    break;
                case Direction.Left:
                    statusString = "Left";
                    break;
                case Direction.Right:
                    statusString = "Right";
                    break;
                case Direction.Forward:
                    statusString = "Forward";
                    break;
                case Direction.Back:
                    statusString = "Back";
                    break;
            }

            lock (depthFrame32)
            {
                // Our player and enemy are both actually just text strings.
                spriteBatch.Begin();

                spriteBatch.Draw(depth, new Vector2(0, 0), Color.White);
                //spriteBatch.Draw(cursorTex, cursorPos,
                //             new Color(TransitionAlpha, TransitionAlpha, TransitionAlpha));
                spriteBatch.DrawString(font, "DeltaX = " + deltaX, textPosX, Color.Black);
                spriteBatch.DrawString(font, "DeltaZ = " + deltaZ, textPosZ, Color.Black);

                spriteBatch.DrawString(font, statusString, textPosStatus, Color.Black);


                string txt = "";
                switch (EnrollmentManager.Players[0].State)
                {
                    case PlayerEnrollmentState.NoSkeleton:
                        txt += "Please step up to the Kinect Sensor";
                        break;

                    case PlayerEnrollmentState.NotInPosition:
                        {
                            switch (EnrollmentManager.Players[0].PositionState)
                            {
                                case OutOfPositionState.NotCentered:
                                    txt += "Please move to the center of the play area";
                                    break;

                                case OutOfPositionState.TooClose:
                                    txt += "Please move back";
                                    break;

                                case OutOfPositionState.TooFar:
                                    txt += "Please move forward";
                                    break;
                            }
                        }
                        break;

                    case PlayerEnrollmentState.WaitingForHandRaise:
                        txt += "Raise your hand to start the game";
                        break;

                    case PlayerEnrollmentState.Enrolled:
                        txt += "Enrolled!";
                        break;

                }

                spriteBatch.DrawString(font, txt, new Vector2(InstructionTextPos.X, InstructionTextPos.Y), Color.White);

                spriteBatch.End();
            }

            graphics.GraphicsDevice.Textures[0] = null;

            base.Draw(gameTime);
        }

        #region Helper Functions
        // Converts a 16-bit grayscale depth frame which includes player indexes into a 32-bit frame
        // that displays different players in different colors
        byte[] convertDepthFrame(byte[] depthFrame16)
        {
            for (int i16 = 0, i32 = 0; i16 < depthFrame16.Length && i32 < depthFrame32.Length; i16 += 2, i32 += 4)
            {
                int player = depthFrame16[i16] & 0x07;
                int realDepth = (depthFrame16[i16 + 1] << 5) | (depthFrame16[i16] >> 3);
                // transform 13-bit depth information into an 8-bit intensity appropriate
                // for display (we disregard information in most significant bit)
                byte intensity = (byte)(255 - (255 * realDepth / 0x0fff));

                depthFrame32[i32 + RED_IDX] = 0;
                depthFrame32[i32 + GREEN_IDX] = 0;
                depthFrame32[i32 + BLUE_IDX] = 0;

                // choose different display colors based on player
                switch (player)
                {
                    case 0:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 2);
                        break;
                    case 1:
                        depthFrame32[i32 + RED_IDX] = intensity;
                        break;
                    case 2:
                        depthFrame32[i32 + GREEN_IDX] = intensity;
                        break;
                    case 3:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 4:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 4);
                        break;
                    case 5:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 6:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 7:
                        depthFrame32[i32 + RED_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(255 - intensity);
                        break;
                }
            }
            return depthFrame32;
        }
        #endregion
    }
}
