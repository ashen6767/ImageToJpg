# ImageToJpg

一个简洁的 Windows 批量图片转 JPG 工具。单 EXE 运行，无需安装第三方运行库。

## 下载

[下载 ImageToJpg 0.1.2](dist/ImageToJpg-0.1.2.exe)

## 功能

- 批量添加图片、文件夹或直接拖放
- 支持 BMP、GIF、ICO、JPEG、PNG、TIFF
- 支持按短边 480px、720px、1080px 等比例缩小
- 小图片不会被放大
- 自动校正 EXIF 拍摄方向
- 可设置 JPEG 输出质量
- 可选择透明区域使用白色或黑色背景
- 尽可能保留图片元数据
- 输出文件重名时自动编号
- 自动记住上次使用的输出目录和转换选项

> JPEG 本身是有损格式，质量 100 只能尽量减少画质损失，无法做到数学意义上的无损转换。

## 系统要求

- Windows 10 或 Windows 11
- 系统自带的 .NET Framework 4.x

## 从源码构建

在 Windows PowerShell 中运行：

```powershell
.\build.ps1
```

生成文件位于 `dist/ImageToJpg-0.1.2.exe`。

## 更新日志

参见 [CHANGELOG.md](CHANGELOG.md)。
