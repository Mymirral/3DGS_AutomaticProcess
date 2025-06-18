using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenCvSharp;

namespace _3DGS_Process
{
    public partial class MainWindowVM : ObservableObject
    {
        [ObservableProperty]
        private string videoPath = "选择视频位置";
        private bool videoSet = false;

        [ObservableProperty]
        private string folderPath = "选择图片保存位置";
        private bool folderSet = false;

        [ObservableProperty]
        private string modelPath = "选择模型生成位置";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ClipCommand))]
        private bool isClipEnable = false;
        bool CanClip() => IsClipEnable = videoSet && folderSet;   //确认是否都选择好了

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AlignPictureAndSaveCommand))]
        private bool isPictureExsist = false;
        bool CanAlign() => IsPictureExsist;                         //是否能输出相机对齐csv

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(Train3DGSCommand))]
        private bool isModelTrainEnable = false;
        bool CanTrain() => IsModelTrainEnable;

        [ObservableProperty]
        private string progress = "等待任务执行";

        [ObservableProperty]
        private Style notifyTextStyle = (Style)Application.Current.Resources["LabelDefault"];   //信息框样式

        /// <summary>
        /// 打开视频流文件，切割成图片序列
        /// </summary>
        [RelayCommand]
        void OpenVideo()    //打开文件
        {
            OpenFileDialog videoDialog = new OpenFileDialog();
            videoDialog.Filter = "视频文件(*.mp4; *.MP4; *.mkv; *.avi)|*.mp4; *.MP4; *.mkv; *.avi";
            videoDialog.ShowDialog();
            if (videoDialog.FileName == "") return;
            VideoPath = videoDialog.FileName;
            videoSet = true;
            CanClip();
        }

        /// <summary>
        /// 打开文件对话框
        /// </summary>
        [RelayCommand]
        void OpenFolder()   //打开文件对话框
        {
            OpenFolderDialog folderDialog = new OpenFolderDialog();
            folderDialog.ShowDialog();
            if (folderDialog.FolderName == "") return;
            FolderPath = folderDialog.FolderName;
            folderSet = true;
            CanClip();
        }

        [RelayCommand]
        void Open3DGSFolder()   //打开文件对话框
        {
            OpenFolderDialog folderDialog = new OpenFolderDialog();
            folderDialog.ShowDialog();
            if (folderDialog.FolderName == "") return;
            ModelPath = folderDialog.FolderName;
            IsModelTrainEnable = true;
            CanClip();
        }


        /// <summary>
        /// 切割视频
        /// </summary>
        /// <returns></returns>
        [RelayCommand(CanExecute = nameof(CanClip))]
        async Task Clip()
        {
            string folderpath = FolderPath;
            string videopath = VideoPath;
            await Task.Run(() =>
            {
                //异步调用一下
                using (var cap = new VideoCapture(videopath))
                {
                    if (!cap.IsOpened())
                    {
                        Progress = "视频打开失败";
                        return;
                    }

                    Progress = "正在切割视频...";
                    string newfolder = Path.Combine(folderpath, Path.GetFileNameWithoutExtension(videopath) + "_Clip");
                    if (!Directory.Exists(newfolder))
                    {
                        Directory.CreateDirectory(newfolder); //创建新文件夹
                    }

                    int i = 0;  //帧索引
                    Mat frame = new Mat();   //新建画布
                    while (cap.Read(frame))   //读取每一帧
                    {
                        Cv2.ImWrite(newfolder + $"\\frame_{i++}.png", frame); //\\{newfolder}
                    }
                    Progress = "分割完成";
                }
            });
            IsPictureExsist = true;
        }

        /// <summary>
        /// 对齐相机参数，并且导出PLY + CSV文件
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAlign))]
        async Task AlignPictureAndSave()
        {
            if (!folderSet)
            {
                await Task.Run(() =>
                {
                    NotifyTextStyle = (Style)Application.Current.Resources["LabelDanger"];
                    Progress = "请选择文件夹后继续！";
                });

                await Task.Delay(2000);
                NotifyTextStyle = (Style)Application.Current.Resources["LabelDefault"];
                Progress = "等待任务执行";
                return;
            }
            string folderpath = FolderPath;
            string videopath = Path.GetFileNameWithoutExtension(VideoPath);

            Progress = "正在对齐相机位置中...";

            await Task.Run(() =>
            {
                //处理各种文件夹路径
                string exePath = @"C:\Program Files\Capturing Reality\RealityCapture\RealityCapture.exe";   //RC的文件路径
                string registrationExportSetting = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "registrationExportSetting.xml");    //相机位置保存设置
                string plyExportSetting = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plyExportSetting.xml");    //点云位置保存设置

                string savefolder;   //保存的文件夹
                if (videoSet) savefolder = $"{Path.Combine(folderpath, videopath)}_Clip";
                else savefolder = folderpath;



                string command =
                    "-headless " +  //不显示界面
                    $"-addFolder \"{savefolder}\" " +
                    "-align " +
                    $"-exportRegistration \"{savefolder}\\test.csv\" {registrationExportSetting} " +
                    $"-exportSparsePointCloud \"{savefolder}\\test.ply\" {plyExportSetting} " +
                    $"-quit";

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,     //文件路径
                    Arguments = command,    //流程
                    CreateNoWindow = true,  //不弹窗
                };

                using (var process = new Process())
                {
                    process.StartInfo = psi;
                    process.Start();
                    process.WaitForExit();
                }
                //Process.Start("explorer.exe", $"{savefolder}");
                Progress = "数据保存完成";
            });
        }

        [RelayCommand]
        void OpenSaveFolder()
        {
            Process.Start("explorer.exe", $"{FolderPath}");
        }

        [RelayCommand(CanExecute = nameof(CanTrain))]
        async Task Train3DGS()
        {
            if (!folderSet)
            {
                await Task.Run(() =>
                {
                    NotifyTextStyle = (Style)Application.Current.Resources["LabelDanger"];
                    Progress = "请选择图片文件夹后继续！";
                });

                await Task.Delay(2000);
                NotifyTextStyle = (Style)Application.Current.Resources["LabelDefault"];
                Progress = "等待任务执行";
                return;
            }

            string picturefolder = FolderPath;
            string savefolder = ModelPath;

            Progress = "正在进行三维高斯溅射...";

            await Task.Run(() =>
            {
                //处理各种文件夹路径
                string exePath = @"C:\Program Files\Jawset Postshot\bin\postshot-cli.exe";   //RC的文件路径

                string command =
                    "train " +
                    $"-i \"{picturefolder}\" \"{picturefolder}\" " +
                    $"-o \"{savefolder}.psht\" ";

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,     //文件路径
                    Arguments = command,    //流程
                    CreateNoWindow = false,  //不弹窗
                };

                using (var process = new Process())
                {
                    process.StartInfo = psi;
                    process.Start();
                    process.WaitForExit();
                }
                //Process.Start("explorer.exe", $"{savefolder}");
                Progress = "模型生成完成";
            });
        }
    }
}
