# MEMENTO

Windows 照片边框工具。

## 概述

MEMENTO 是一个轻量级的照片边框工具，自动读取 EXIF 信息，用于快速为照片添加底部信息栏。信息栏可显示相机型号、镜头参数、拍摄日期及自定义摄影师名称，生成的边框图片可直接保存为 JPG 格式。

## 技术栈

- .NET 10
- WPF
- MetadataExtractor
- TagLibSharp

## 系统要求

- Windows 10 版本 1809 或更高
- Windows 11
- .NET 10 Desktop Runtime（如使用独立发布版本则无需安装）

## 功能

**文件管理**

- 选择文件夹，程序自动扫描支持格式的照片
- 支持的格式：.jpg、.jpeg、.png、.bmp、.gif
- 按评分筛选显示（与 MARKABLE 协同）

**浏览**

- 左侧区域缩略图列表
- 右侧区域大图预览
- 前后切换按钮
- 在键盘上按下左右箭头键切换

**边框与信息**

- 自动读取 EXIF 信息：相机型号、镜头参数、拍摄日期
- 自定义摄影师姓名
- 底部白条高度可调节
- 多种字体选择（支持自定义字体）
- 可隐藏相机信息或拍摄参数

**性能**

- 异步加载
- 缩略图缓存
- 虚拟化列表

## 与 MARKABLE 协同

MEMENTO 可与 [MARKABLE](https://github.com/SpFirefly/MARKABLE) 协同工作：
- 读取 MARKABLE 写入照片的评分数据
- 支持按最低评分筛选照片

## 安装与运行

**方式一：下载可执行文件**

前往 Releases 页面下载 `MEMENTO.exe`，双击运行。若提示缺少 .NET Runtime，需先安装 .NET 10 Desktop Runtime。

**方式二：从源码编译**

```bash
git clone https://github.com/SpFirefly/MEMENTO.git
cd MEMENTO
dotnet build -c Release
```

编译产物位于 `bin/Release/net10.0-windows/`。

## 使用

1. 点击「打开文件夹」，选择包含照片的目录
2. 左侧列表显示所有照片，点击任意照片查看大图
3. 在右侧设置区域填写摄影师姓名、调节白条高度、选择字体
4. 勾选显示选项（相机信息/拍摄参数）
5. 点击「保存 JPG」输出带边框的图片

## 已知限制 (v1.0)

- 仅支持 JPEG、PNG、BMP、GIF 格式
- RAW 格式仅读取预览图，不支持边框生成
- EXIF 读取依赖照片元数据，部分老旧或无元数据的照片可能无法显示完整信息

## 许可证

MIT License

版权 (c) 2026 Jerry Shi (a.k.a. SpFirefly)

## 作者

GitHub: [@SpFirefly](https://github.com/SpFirefly)
