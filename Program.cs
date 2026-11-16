using Raylib_cs;
using System.Numerics;
using System.Diagnostics;

namespace _100MillionPixels
{
    public class Program
    {
        const float CameraSpeed = 500.0f;
        const float ZoomAnimationSpeed = 30.0f;

        static Camera2D camera = new Camera2D(Vector2.Zero, new Vector2(World.CenterX, World.CenterY), 0.0f, 10.0f);

        static void Main()
        {
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
            Raylib.InitWindow(800, 600, "sosat amerika");

            World world = new World();
            float cameraTargetZoom = camera.Zoom;

            float timeScale = 1.0f;

            List<Ball> balls = new List<Ball>();
            Vector2 startDragBalls = Vector2.Zero;

            BallType selectedBall = BallType.PIERCING;

            world.Generate();

            world.Tick((float)Raylib.GetTime());

            OnWindowResize();
            while (!Raylib.WindowShouldClose())
            {
                Raylib.SetWindowTitle(Raylib.GetFPS().ToString());

                if (Raylib.IsWindowResized()) OnWindowResize();

                for (int key = 1; key <= 4; key++)
                {
                    if (Raylib.IsKeyPressed(KeyboardKey.Zero + key))
                    {
                        selectedBall = (BallType)(key - 1);
                    }
                }

                float dt = Raylib.GetFrameTime();

                Vector2 movingVector = new Vector2(
                    Raylib.IsKeyDown(KeyboardKey.D) - Raylib.IsKeyDown(KeyboardKey.A),
                    Raylib.IsKeyDown(KeyboardKey.S) - Raylib.IsKeyDown(KeyboardKey.W)
                );
                if (movingVector != Vector2.Zero) movingVector = Vector2.Normalize(movingVector);
                camera.Target += movingVector * dt * CameraSpeed / camera.Zoom;

                float wheel = Raylib.GetMouseWheelMoveV().Y;
                if (Raylib.IsKeyDown(KeyboardKey.G))
                {
                    float timeScaleSpeed = 1.25f;
                    timeScale *= MathF.Pow(timeScaleSpeed, wheel);
                    //timeScale += wheel * 0.25f;

                    timeScale = Raymath.Clamp(timeScale, 0.0047223665f, 3081.4879f);

                    wheel = 0.0f;
                }

                const float zoomSpeed = 1.25f;
                if (wheel != 0.0f)
                {
                    const float defaultZoom = 2.0f;
                    float maxZoom = defaultZoom * MathF.Pow(zoomSpeed, 20);

                    cameraTargetZoom *= MathF.Pow(zoomSpeed, wheel);
                    cameraTargetZoom = Math.Clamp(cameraTargetZoom, defaultZoom, maxZoom);
                }
                float zoomBefore = camera.Zoom;
                camera.Zoom = Raymath.Lerp(camera.Zoom, cameraTargetZoom, Raymath.Clamp01(dt * ZoomAnimationSpeed));
                float zoomAfter = camera.Zoom;
                if (zoomBefore != zoomAfter)
                {
                    Vector2 mouseScreen = Raylib.GetMousePosition();
                    camera.Target += (mouseScreen - camera.Offset) * (1.0f / zoomBefore - 1.0f / zoomAfter);
                }

                world.Tick(dt);

                foreach (Ball ball in balls)
                {
                    ball.Tick(dt * timeScale, world);
                }

                if (Raylib.IsMouseButtonDown(MouseButton.Middle) || Raylib.IsMouseButtonDown(MouseButton.Right))
                {
                    Vector2 delta = Raylib.GetMouseDelta();
                    camera.Target -= delta / camera.Zoom;
                }

                Vector2 mouseWorld = GetMouseWorldPosition();

                Vector2 drag = Vector2.Zero;
                if (Raylib.IsMouseButtonPressed(MouseButton.Left)) startDragBalls = mouseWorld;
                if (Raylib.IsMouseButtonReleased(MouseButton.Left))
                {
                    drag = mouseWorld - startDragBalls;
                    //Vector2 velocity = -drag * 10.0f;
                    Vector2 velocity = -drag + Vector2.One * 0.01f;

                    balls.Add(new Ball(selectedBall, startDragBalls, velocity));
                }

                Raylib.BeginDrawing();

                Raylib.ClearBackground(new Color(45, 40, 50));

                Raylib.BeginMode2D(camera);

                //Raylib.DrawRectangleLines(0, 0, World.Width, World.Height, Color.White);

                Rectangle viewRect = GetCameraViewRect(camera);
                world.Draw(viewRect);

                foreach (Ball ball in balls)
                {
                    ball.Draw();
                }

                //Raylib.DrawText("This is the\nWorld Center", WorldCenterX, WorldCenterY, 10, Color.DarkGray);

                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                    Raylib.DrawLineV(startDragBalls, GetMouseWorldPosition(), Color.Lime);

                Raylib.EndMode2D();

                DrawTextOutline($"Selected: {selectedBall}", 10, 10, 40, Ball.GetColor(selectedBall));

                if (MathF.Abs(timeScale - 1.0f) > 0.01f)
                {
                    DrawTextOutline($"x{timeScale:F1}", 10, 50, 40, Color.Yellow);
                }


                Raylib.EndDrawing();
            }

            Raylib.CloseWindow();
        }

        const int TextShadowDistance = 4;

        public static void DrawTextShadow(string text, int posX, int posY, int fontSize, Color color)
        {
            Raylib.DrawText(text, posX + TextShadowDistance, posY + TextShadowDistance, fontSize, Color.Black);
            Raylib.DrawText(text, posX, posY, fontSize, color);
        }

        public static void DrawTextOutline(string text, int posX, int posY, int fontSize, Color color)
        {
            Raylib.DrawText(text, posX + TextShadowDistance, posY + TextShadowDistance, fontSize, Color.Black);
            Raylib.DrawText(text, posX - TextShadowDistance, posY + TextShadowDistance, fontSize, Color.Black);
            Raylib.DrawText(text, posX + TextShadowDistance, posY - TextShadowDistance, fontSize, Color.Black);
            Raylib.DrawText(text, posX - TextShadowDistance, posY - TextShadowDistance, fontSize, Color.Black);

            Raylib.DrawText(text, posX + TextShadowDistance, posY, fontSize, Color.Black);
            Raylib.DrawText(text, posX - TextShadowDistance, posY, fontSize, Color.Black);
            Raylib.DrawText(text, posX, posY + TextShadowDistance, fontSize, Color.Black);
            Raylib.DrawText(text, posX, posY - TextShadowDistance, fontSize, Color.Black);

            Raylib.DrawText(text, posX, posY, fontSize, color);
        }

        public static void OnWindowResize()
        {
            int screenWidth = Raylib.GetScreenWidth();
            int screenHeight = Raylib.GetScreenHeight();

            camera.Offset = new Vector2(screenWidth / 2, screenHeight / 2);
        }

        public static Vector2 GetMouseWorldPosition()
        {
            return Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), camera);
        }

        public static Rectangle GetCameraViewRect(Camera2D camera)
        {
            Vector2[] screenCorners = {
                Vector2.Zero,
                new Vector2(Raylib.GetScreenWidth(), 0),
                new Vector2(0, Raylib.GetScreenHeight()),
                new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight())
            };

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (Vector2 corner in screenCorners)
            {
                Vector2 world = Raylib.GetScreenToWorld2D(corner, camera);
                if (world.X < minX) minX = world.X;
                if (world.Y < minY) minY = world.Y;
                if (world.X > maxX) maxX = world.X;
                if (world.Y > maxY) maxY = world.Y;
            }

            Rectangle viewRect = new Rectangle(minX, minY, maxX - minX, maxY - minY);

            return viewRect;
        }
    }
}
