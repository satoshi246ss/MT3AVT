﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace MT3
{
    partial class Form1 //uEye
    {
        /// <summary>
        /// 画像表示ルーチン
        /// </summary>
        /// <param name="capacity">画像表示用タイマールーチン</param>
        private void ids_timerDisplay_Tick(object sender, EventArgs e)
        {
            //uEye.Camera Camera = sender as uEye.Camera;
            //      if (this.States == STOP || cam == null) return;

            // IDS　表示ルーチン（多分高速）
            //Int32 s32MemID;
            //if (cam.Memory.GetActive(out s32MemID) == uEye.Defines.Status.SUCCESS && cam.IsOpened)
            //{
            //    cam.Display.DisplayImage.Set(s32MemID, u32DisplayID, uEye.Defines.DisplayRenderMode.Normal);//FitToWindow);
            //}
        }

        public void OpenIDScamera()//(object sender, EventArgs e)
        {
            // IDS camera check
            int NumberOfCameras;
            uEye.Info.Camera.GetNumberOfDevices(out NumberOfCameras);
            if (NumberOfCameras <= 0) Environment.Exit(0);

            uEye.Types.CameraInformation[] cameraList;
            uEye.Info.Camera.GetCameraList(out cameraList);

            statusRet = uEye.Defines.Status.NO_SUCCESS;
            foreach (uEye.Types.CameraInformation info in cameraList)
            {
                if (info.CameraID == cameraID)
                {
                    statusRet = uEye.Defines.Status.SUCCESS;
                }
                /*ListViewItem item = new ListViewItem();
                item.Text = info.InUse ? "No" : "Yes";
                item.ImageIndex = info.InUse ? 1 : 0;

                item.SubItems.Add(info.CameraID.ToString());
                item.SubItems.Add(info.DeviceID.ToString());
                item.SubItems.Add(info.Model);
                item.SubItems.Add(info.SerialNumber);
            */
            }

            // Open Camera
            if (statusRet == uEye.Defines.Status.SUCCESS)
            {
                statusRet = cam.Init(cameraID);
                if (statusRet != uEye.Defines.Status.SUCCESS)
                {
                    //MessageBox.Show("IDS Camera initializing failed");
                    Environment.Exit(0);
                }

                // PixelFormat MONO 8
                uEye.Defines.ColorMode mode;
                cam.PixelFormat.Get(out mode);
                cam.PixelFormat.Set(uEye.Defines.ColorMode.MONO8);
                cam.PixelFormat.Get(out mode);

                // Allocate Memory
                statusRet = cam.Memory.Allocate(out s32MemID, true);
                if (statusRet != uEye.Defines.Status.SUCCESS)
                {
                    MessageBox.Show("IDS Allocate Memory failed");
                }

                // Connect Event
                cam.EventFrame += onFrameEvent;

                // Set PC,Fr,Exp
                statusRet = cam.Timing.PixelClock.Set(set_pixelclock);
                if (statusRet != uEye.Defines.Status.SUCCESS)
                {
                    MessageBox.Show("IDS Camera Set PixelClock failed");
                }

                statusRet = cam.Timing.Exposure.Set(set_exposure);
                if (statusRet != uEye.Defines.Status.SUCCESS)
                {
                    MessageBox.Show("IDS Camera Set Exposure failed");
                }

                statusRet = cam.Timing.Framerate.Set(set_framerate);
                if (statusRet != uEye.Defines.Status.SUCCESS)
                {
                    MessageBox.Show("IDS Camera Set FrameRate failed");
                }

                cam.Gain.Hardware.Scaled.SetMaster(set_gain);
            }
        }
        // IDS event追加
        private void onFrameEvent1(object sender, EventArgs e)
        {
            uEye.Camera Camera = sender as uEye.Camera;

            Int32 s32MemID;
            Camera.Memory.GetActive(out s32MemID);

            Camera.Display.DisplayImage.Set(s32MemID, u32DisplayID, uEye.Defines.DisplayRenderMode.FitToWindow);
            ++id;
        }
        //
        // 毎フレーム呼び出し(75fr/s)
        // IDS event追加
        private void onFrameEvent(object sender, EventArgs e)
        {
            sw.Start();
            Int32 s32MemID;
            cam.Memory.GetActive(out s32MemID);
            System.IntPtr ptr;

            //画像データをバッファにコピー
            cam.Memory.Lock(s32MemID);
            uEye.Types.ImageInfo imageInfo;
            cam.Information.GetImageInfo(s32MemID, out imageInfo);
            cam.Memory.ToIntPtr(s32MemID, out ptr);
            CopyMemory( img_dmk.ImageDataOrigin, ptr,img_dmk.ImageSize);
            Cv.Copy(img_dmk, imgdata.img);
            elapsed0 = sw.ElapsedTicks; // 0.1ms

            //3  Cv.Copy(img_dmk3, imgdata.img);
            //Cv.Copy(img_dmk, imgdata.img);

            //img_dmk3.ImageData = ptr;
            //Cv.CvtColor(img_dmk3, imgdata.img, ColorConversion.BgrToGray); // 遅い er:2.6%
            //Cv.Split(img_dmk3, imgdata.img, null,null,null); // er:1.1%
            cam.Memory.Unlock(s32MemID);

            detect();
        }

        // 毎フレーム呼び出し(120fr/s)
        // IDS event追加
        private void onFrameEvent2(object sender, EventArgs e)
        {
            sw.Start();
            double framerate0 = 0, framerate1 = 0;//, alfa_fr = 0.99;
            Int32 s32MemID;
            cam.Memory.GetActive(out s32MemID);
            try
            {
                System.IntPtr ptr;
                cam.Memory.Lock(s32MemID);
                uEye.Types.ImageInfo imageInfo;
                cam.Information.GetImageInfo(s32MemID, out imageInfo);
                cam.Memory.ToIntPtr(s32MemID, out ptr);
                CopyMemory(img_dmk3.ImageDataOrigin, ptr, img_dmk3.ImageSize);
                Cv.CvtColor(img_dmk3, img_dmk, ColorConversion.BgrToGray);
                cam.Memory.Unlock(s32MemID);

                //Cv.Copy(img_dmk, img2, null);

                ++id;
                elapsed0 = sw.ElapsedTicks;

                // 保存用データをキューへ
                if (ImgSaveFlag == TRUE)
                {
                    double min_val, max_val;
                    CvPoint min_loc, max_loc;
                    int size = 15;
                    int size2x = size / 2;
                    int size2y = size / 2;
                    //int num    = 0;
                    double sigma = 3;

                    // 位置検出
                    Cv.Smooth(img_dmk, img2, SmoothType.Gaussian, size, 0, sigma, 0);
                    CvRect rect = new CvRect(1, 1, WIDTH - 2, HEIGHT - 2);
                    Cv.SetImageROI(img2, rect);
                    Cv.MinMaxLoc(img2, out  min_val, out  max_val, out  min_loc, out  max_loc, null);
                    Cv.ResetImageROI(img2);
                    max_loc.X += 1; // 基準点が(1,1)のため＋１
                    max_loc.Y += 1;

                    double m00, m10, m01;
                    if (max_loc.X - size2x < 0) size2x = max_loc.X;
                    if (max_loc.Y - size2y < 0) size2y = max_loc.Y;
                    if (max_loc.X + size2x >= WIDTH) size2x = WIDTH - max_loc.X - 1;
                    if (max_loc.Y + size2y >= HEIGHT) size2y = HEIGHT - max_loc.Y - 1;
                    rect = new CvRect(max_loc.X - size2x, max_loc.Y - size2y, size, size);
                    CvMoments moments;
                    Cv.SetImageROI(img2, rect);
                    Cv.Moments(img2, out moments, false);
                    Cv.ResetImageROI(img2);
                    m00 = Cv.GetSpatialMoment(moments, 0, 0);
                    m10 = Cv.GetSpatialMoment(moments, 1, 0);
                    m01 = Cv.GetSpatialMoment(moments, 0, 1);
                    gx = max_loc.X - size2x + m10 / m00;
                    gy = max_loc.Y - size2y + m01 / m00;

                    //    Pid_Data_Send();
                    elapsed1 = sw.ElapsedTicks;
                    imgdata_push_FIFO();
                }
            }
            catch (Exception ex)
            {
                //匿名デリゲートで表示する
                this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, ex.ToString() });
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
            finally
            {
            }
            elapsed2 = sw.ElapsedTicks; sw.Stop(); sw.Reset();
            // 処理速度
            //  Double dFramerate;
            //  cam.Timing.Framerate.GetCurrentFps(out dFramerate);
            //  toolStripStatusLabelFramerate.Text = "Fps: " + dFramerate.ToString("00.00");

            //framerate0 = alfa_fr * framerate1 + (1 - alfa_fr) * (Stopwatch.Frequency / (double)elapsed2);
            //  framerate0 = ++id_fr / (this.icImagingControl1.ReferenceTimeCurrent - this.icImagingControl1.ReferenceTimeStart);
            framerate1 = framerate0;

            double sf = (double)Stopwatch.Frequency / 1000; //msec
            fr_str = String.Format("ID:{0,5:D1} L0:{1,4:F2} L1:{2,4:F2} L2:{3,4:F2} fr:{4,5:F1}", id, elapsed0 / sf, elapsed1 / sf, elapsed2 / sf, framerate0);
            // 文字入れ
            //string st = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            //img_dmk.PutText(st, new CvPoint(10, 470), font, new CvColor(0, 100, 100));
        }

    }
}
