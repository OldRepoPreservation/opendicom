/*
    openDICOM.NET Navigator 0.1.0

    Simple GTK ACR-NEMA and DICOM Viewer for Mono / .NET based on the 
    openDICOM.NET library.

    Copyright (C) 2006  Albert Gnandt

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA

*/
using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections;
using Gtk;
using Glade;
using Gdk;
using GLib;
using openDicom.Registry;
using openDicom.DataStructure;
using openDicom.DataStructure.DataSet;
using openDicom.Encoding.Type;
using openDicom.File;


public sealed class MainWindow: GladeWidget
{
    public new Gtk.Window Self
    {
        get { return (Gtk.Window) base.Self; }
    }

    private AcrNemaFile dicomFile = null;
    public AcrNemaFile DicomFile
    {
        get { return dicomFile; }
    }

    private string defaultTitle = null;

    private byte[][] images;
    private int imageIndex = 0;
    private double scaleFactor = 1.0;
    private bool isSlideCycling = false;
    private uint slideCyclingIdleHandler;

    private const double scaleStep = 0.10;
    private const double minScaleFactor = 0.10;
    private const double maxScaleFactor = 8.0;

    private const double brightnessStep = 0.05;
    private const double minBrightnessFactor = 0.0;
    private const double maxBrightnessFactor = 2.0;

    private bool CorrectIndex
    {
        get { return DicomFile.PixelData.Data.Value.IsSequence; }
    }

    [WidgetAttribute]
    TreeView MainTreeView;

    [WidgetAttribute]
    TreeView MainFileInfoTreeView;

    [WidgetAttribute]
    Gtk.Image ImageViewImage;

    [WidgetAttribute]
    Viewport ImageViewViewport;

    [WidgetAttribute]
    Statusbar ImageViewStatusbar;

    [WidgetAttribute]
    HScale CyclingSpeedHScale;
    
    [WidgetAttribute]
    Label TimeUnitLabel;

    [WidgetAttribute]
    ToolButton FirstSlideToolButton;

    [WidgetAttribute]
    ToolButton PrevSlideToolButton;

    [WidgetAttribute]
    ToolButton CycleSlidesToolButton;

    [WidgetAttribute]
    ToolButton NextSlideToolButton;

    [WidgetAttribute]
    ToolButton LastSlideToolButton;

    [WidgetAttribute]
    ToolButton ZoomInToolButton;

    [WidgetAttribute]
    ToolButton OriginalSizeToolButton;

    [WidgetAttribute]
    ToolButton FitWindowToolButton;

    [WidgetAttribute]
    ToolButton ZoomOutToolButton;

    [WidgetAttribute]
    ToolButton OpenToolButton;

    [WidgetAttribute]
    ToolButton PreferencesToolButton;

    [WidgetAttribute]
    ToolButton SaveImageToolButton;

    [WidgetAttribute]
    ToolButton ExportAsToolButton;

    [WidgetAttribute]
    ToolButton BrightenToolButton;
    
    [WidgetAttribute]
    ToolButton DarkenToolButton;

    [WidgetAttribute]
    ToolButton GimpToolButton;

    [WidgetAttribute]
    ImageMenuItem OpenMenuItem;

    [WidgetAttribute]
    ImageMenuItem SaveImageMenuItem;

    [WidgetAttribute]
    ImageMenuItem PreferencesMenuItem;

    [WidgetAttribute]
    ImageMenuItem ExportAsMenuItem;


    public MainWindow(string fileName): base("MainWindow")
    {
        Self.Icon = Pixbuf.LoadFromResource(Resources.IconResource);
        BrightenToolButton.IconWidget = Gtk.Image.LoadFromResource(
            Resources.StockBrightenResource);
        BrightenToolButton.IconWidget.Show();
        DarkenToolButton.IconWidget = Gtk.Image.LoadFromResource(
            Resources.StockDarkenResource);
        DarkenToolButton.IconWidget.Show();
        GimpToolButton.IconWidget = Gtk.Image.LoadFromResource(
            Resources.GimpIconResource);
        GimpToolButton.IconWidget.Show();
        defaultTitle = Self.Title;
        CyclingSpeedHScale.Value = Configuration.Global.SlideCyclingSpeed;
        SaveImageMenuItem.Sensitive = false;
        SaveImageToolButton.Sensitive = false;
        ExportAsMenuItem.Sensitive = false;
        ExportAsToolButton.Sensitive = false;
        FirstSlideToolButton.Sensitive = false;
        PrevSlideToolButton.Sensitive = false;
        CycleSlidesToolButton.Sensitive = false;
        TimeUnitLabel.Sensitive = false;
        NextSlideToolButton.Sensitive = false;
        LastSlideToolButton.Sensitive = false;
        ZoomInToolButton.Sensitive = false;
        OriginalSizeToolButton.Sensitive = false;
        FitWindowToolButton.Sensitive = false;
        ZoomOutToolButton.Sensitive = false;
        CyclingSpeedHScale.Sensitive = false;
        BrightenToolButton.Sensitive = false;
        DarkenToolButton.Sensitive = false;
        GimpToolButton.Sensitive = false;
        MainTreeView.AppendColumn("Tag", new CellRendererText(), "text", 0);
        MainTreeView.AppendColumn("Description", new CellRendererText(), 
            "text", 1);
        MainTreeView.AppendColumn("Value", new CellRendererText(), "text", 
            2);
        MainTreeView.AppendColumn("Value Representation", 
            new CellRendererText(), "text", 3);
        MainTreeView.AppendColumn("Value Multiplicity",
            new CellRendererText(), "text", 4);
        MainTreeView.AppendColumn("Value Length", new CellRendererText(),
            "text", 5);
        MainTreeView.AppendColumn("Stream Position", new CellRendererText(),
            "text", 6);
        MainFileInfoTreeView.AppendColumn("Key", new CellRendererText(),
            "text", 0);
        MainFileInfoTreeView.AppendColumn("Value", new CellRendererText(),
            "text", 1);
        LoadDicomFile(fileName);
    }

    private void OnMainWindowDeleteEvent(object o, DeleteEventArgs args)
    {
        Configuration.Global.SlideCyclingSpeed = (int) CyclingSpeedHScale.Value;
        Application.Quit();
        args.RetVal = true;
    }

    private void OnQuitMenuItemActivate(object o, EventArgs args) 
    {
        Configuration.Global.SlideCyclingSpeed = (int) CyclingSpeedHScale.Value;
        Application.Quit();
    }

    private void OnAboutMenuItemActivate(object o, EventArgs args) 
    {
        AboutDialog d = new AboutDialog();
        d.Self.Run();
    }

    private void LoadDicomFile(string fileName)
    {
        if (fileName != null && fileName != "")
        {
            if (File.Exists(fileName))
            {
                if (Configuration.Global.AreDictionariesAvailable)
                {
                    bool useStrictDecoding = 
                        Configuration.Global.UseStrictDecoding;
                    try
                    {
                        if (openDicom.File.DicomFile.IsDicomFile(fileName))
                        {
                            dicomFile = new DicomFile(fileName, 
                                useStrictDecoding);
                            PostDicomFileLoad(fileName);
                        }
                        else if (AcrNemaFile.IsAcrNemaFile(fileName))
                        {
                            dicomFile = new AcrNemaFile(fileName,
                                useStrictDecoding);
                            PostDicomFileLoad(fileName);
                        }
                        else if (XmlFile.IsXmlFile(fileName))
                        {
                            MessageDialog(MessageType.Error,
                                "Found DICOM-/ACR-NEMA-XML file instead of " +
                                "DICOM file. This function is not implemented.");
                        }
                        else
                        {
                            MessageDialog(MessageType.Error,
                                "User specified file is whether " +
                                "a DICOM, ACR-NEMA nor a compliant XML file.");
                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionDialog d = new ExceptionDialog(
                            "Unexpected problems loading file.", e);
                        d.Self.Run();
                    }
                }
                else
                    MessageDialog(MessageType.Error,
                        "Please select valid data dictionaries " +
                        "before loading. Use the preferences dialog for " +
                        "registration.");
            }
            else
                MessageDialog(MessageType.Error,
                    "User specified file does not exists.");
        }
    }

    private void PrepareImages()
    {
        if (DicomFile.HasPixelData)
            images = DicomFile.PixelData.ToBytesArray();
        Tag numberOfFramesTag = new Tag("0028", "0008");
        if (DicomFile.DataSet.Contains(numberOfFramesTag))
        {
            int frames = int.Parse(
                DicomFile.DataSet[numberOfFramesTag].Value[0].ToString());
            if (frames > 1 && 
                ((CorrectIndex && images.Length == 2) || images.Length == 1))
            {
                byte[] buffer = images[images.Length - 1];
                int size = buffer.Length / frames;
                byte[][] results = new byte[frames][];
                int i = 0;
                for (i = 0; i < frames; i++)
                {
                    results[i] = new byte[size];
                    Array.Copy(buffer, i * size, results[i], 0, size);
                }
                if (CorrectIndex)
                {
                    buffer = images[0];
                    images = new byte[frames + 1][];
                    images[0] = buffer;
                    for (i = 0; i < frames; i++)
                        images[i + 1] = results[i];
                }
                else    
                    images = results;
            }
        }
        if (DicomFile.DataSet.TransferSyntax.Uid.Equals("1.2.840.10008.1.2.5"))
        {
            // RLE
            int startIndex = CorrectIndex ? 1 : 0;
            byte[][] tempImages = images;
            try
            {
                for (int i = startIndex; i < images.Length; i++)
                    images[i] = DecodeRLE(images[i]);
            }
            catch (Exception e)
            {
                images = tempImages;
                tempImages = null;
                System.GC.Collect();
                MessageDialog(MessageType.Error, 
                    "Unable to RLE decode images.");
            }
        }
    }

    private byte[] DecodeRLE(byte[] buffer)
    {
        // Implementation of the DICOM 3.0 2004 RLE Decoder
        ulong[] header = new ulong[16];
        for (int i = 0; i < header.Length; i++)
            header[i] = BitConverter.ToUInt64(buffer, i * 4);
        int numberOfSegments = 1;
        if (header[0] > 1 && header[0] <= (ulong) header.LongLength - 1)
            numberOfSegments = (int) header[0];
        ulong[] offsetOfSegment = new ulong[numberOfSegments];
        Buffer.BlockCopy(header, 1, offsetOfSegment, 0, numberOfSegments);
        ulong[] sizeOfSegment = new ulong[numberOfSegments];
        int sizeSum = 0;
        for (int i = 0; i < numberOfSegments - 1; i++)
        {
            sizeOfSegment[i] = offsetOfSegment[i + 1] - offsetOfSegment[i];
            sizeSum += (int) sizeOfSegment[i];
        }
        sizeOfSegment[numberOfSegments - 1] =
            (ulong) buffer.LongLength - offsetOfSegment[numberOfSegments - 1];
        sizeSum += (int) sizeOfSegment[numberOfSegments - 1];
        ArrayList resultBuffer = new ArrayList(2 * sizeSum);
        ArrayList byteSegment = new ArrayList();
        for (int i = 0; i < numberOfSegments; i++)
        {
            int offset = (int) offsetOfSegment[i];
            int size = (int) sizeOfSegment[i];
            byte[] rleSegment = new byte[size];
            Buffer.BlockCopy(buffer, offset, rleSegment, 0, size);
            byteSegment.Capacity = 2 * size;
            sbyte n;
            int rleIndex = 0;
            while (rleIndex < size)
            {
                n = (sbyte) rleSegment[rleIndex];
                if (n >= 0 && n <= 127)
                {
                    for (int j = 0; j < n; j++)
                    {
                        rleIndex++;
                        if (rleIndex >= size) break;
                        byteSegment.Add(rleSegment[rleIndex]);
                    }
                }
                else if (n <= -1 && n >= -127)
                {
                    rleIndex++;
                    if (rleIndex >= size) break;
                    for (int j = 0; j < -n; j++)
                        byteSegment.Add(rleSegment[rleIndex]);
                }
                rleIndex++;
            }
            resultBuffer.AddRange(byteSegment);
            byteSegment.Clear();
        }
        byte[] result = (byte[]) resultBuffer.ToArray(typeof(byte));
        resultBuffer.Clear();
        return result;
    }

    private void PostDicomFileLoad(string fileName)
    {
        Self.Title = defaultTitle + " - " + Path.GetFileName(fileName);
        ImageViewImage.Pixbuf = null;
        RefreshFileInfoTreeView(fileName);
        RefreshDicomTreeView();
        PrepareImages();
        imageIndex = 0;
        scaleFactor = 1.0;
        ShowImage();
        if (images.Length == 1 || (CorrectIndex && images.Length == 2))
        {
            FirstSlideToolButton.Sensitive = false;
            PrevSlideToolButton.Sensitive = false;
            CycleSlidesToolButton.Sensitive = false;
            NextSlideToolButton.Sensitive = false;
            LastSlideToolButton.Sensitive = false;
            CyclingSpeedHScale.Sensitive = false;
            TimeUnitLabel.Sensitive = false;
        }
        else
        {
            FirstSlideToolButton.Sensitive = true;
            PrevSlideToolButton.Sensitive = true;
            CycleSlidesToolButton.Sensitive = true;
            NextSlideToolButton.Sensitive = true;
            LastSlideToolButton.Sensitive = true;
            CyclingSpeedHScale.Sensitive = true;
            TimeUnitLabel.Sensitive = true;
        }
        ExportAsMenuItem.Sensitive = true;
        ExportAsToolButton.Sensitive = true;
    }

    private void OnOpenMenuItemActivate(object o, EventArgs args) 
    {
        if (Configuration.Global.AreDictionariesAvailable)
        {
            GenericOpenFileChooserDialog openFileChooserDialog = 
                new GenericOpenFileChooserDialog("Open ACR-NEMA or DICOM file");
            openFileChooserDialog.Self.Run();
            if (File.Exists(openFileChooserDialog.FileName))
                LoadDicomFile(openFileChooserDialog.FileName);
        }
        else
            MessageDialog(MessageType.Error,
                "Please select valid data dictionaries at " +
                "preferences before loading.");
    }

    private void OnSaveImageMenuItemActivate(object o, EventArgs args) 
    {
        SaveImageFileChooserDialog saveImageFileChooserDialog = 
            new SaveImageFileChooserDialog();
        int response = saveImageFileChooserDialog.Self.Run();
        if (response == -5)
        {
            string imageFileName = saveImageFileChooserDialog.FileName;
            string imageFileType = saveImageFileChooserDialog.FileType;
            try
            {
                ImageViewImage.Pixbuf.Save(imageFileName, imageFileType);
            }
            catch (Exception e)
            {
                ExceptionDialog d = new ExceptionDialog(
                  "Unexpected problems saving image.", e);
                d.Self.Run();
            }
        }
    }

    private void DeactivateImageView()
    {
        if (isSlideCycling)
            OnCycleSlidesToolButtonClicked(null, null);
        SaveImageMenuItem.Sensitive = false;
        SaveImageToolButton.Sensitive = false;
        //BrightenToolButton.Sensitive = false;
        //DarkenToolButton.Sensitive = false;
        GimpToolButton.Sensitive = false;
        ZoomInToolButton.Sensitive = false;
        OriginalSizeToolButton.Sensitive = false;
        FitWindowToolButton.Sensitive = false;
        ZoomOutToolButton.Sensitive = false;
    }

    private void ActivateImageView()
    {
        if ( ! isSlideCycling)
        {
            SaveImageMenuItem.Sensitive = true;
            SaveImageToolButton.Sensitive = true;
            //BrightenToolButton.Sensitive = true;
            //DarkenToolButton.Sensitive = true;
            GimpToolButton.Sensitive = true;
        }
        ZoomInToolButton.Sensitive = true;
        OriginalSizeToolButton.Sensitive = true;
        FitWindowToolButton.Sensitive = true;
        ZoomOutToolButton.Sensitive = true;
    }

    private Gdk.Image BrightenImage(Gdk.Image src, double brightnessFactor)
    {
        // untested!
        Gdk.Image image = src;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                uint col = image.GetPixel(x, y);
                col = (uint) (((double) col) * brightnessFactor);
                image.PutPixel(x, y, col);
            }
        }
        return image;
    }

    private void ShowImage()
    {
        if (DicomFile.HasPixelData)
        {
            try
            {
                if (imageIndex < 0)
                    imageIndex = 0;
                else if (imageIndex >=  images.Length)
                    imageIndex = images.Length - 1;
                if (CorrectIndex && imageIndex == 0)
                    imageIndex = 1;
                if (scaleFactor < minScaleFactor)
                    scaleFactor = minScaleFactor;
                else if (scaleFactor > maxScaleFactor)
                    scaleFactor = maxScaleFactor;
                if (Configuration.Global.ImageBrightnessFactor < 
                    minBrightnessFactor)
                    Configuration.Global.ImageBrightnessFactor = 
                        minBrightnessFactor;
                else if (Configuration.Global.ImageBrightnessFactor > 
                    maxBrightnessFactor)
                    Configuration.Global.ImageBrightnessFactor = 
                        maxBrightnessFactor;
                ImageViewStatusbar.Push(0, string.Format(
                    "Image: {0}/{1}, Zoom: {2}%, Brightness: {3}%", 
                    CorrectIndex ? imageIndex : imageIndex + 1,
                    CorrectIndex ? images.Length - 1 : images.Length,
                    (int) (scaleFactor * 100),
                    (int) ((Configuration.Global.ImageBrightnessFactor / 2) * 
                        100)));
                bool isJpegNotSupported = false;
                try
                {
                    ImageViewImage.Pixbuf = new Pixbuf(images[imageIndex],
                            DicomFile.PixelData.Columns,
                            DicomFile.PixelData.Rows);
                }
                catch (Exception exception)
                {
                    // fallback solution
                    if (DicomFile.PixelData.IsJpeg)
                    {
                        isJpegNotSupported = true;
                        DeactivateImageView();
                        MessageDialog(MessageType.Error,
                            string.Format("JPEG format of image {0}/{1} is " +
                                "not supported.",
                                CorrectIndex ? imageIndex : imageIndex + 1, 
                                CorrectIndex ? images.Length - 1 : 
                                    images.Length));
                    }
                    else
                    {
                        ImageViewImage.Pixbuf = new Pixbuf(images[imageIndex],
                            false,
                            DicomFile.PixelData.BitsStored,
                            DicomFile.PixelData.Columns,
                            DicomFile.PixelData.Rows,
                            DicomFile.PixelData.Columns * 
                                (DicomFile.PixelData.BitsAllocated / 8),
                            null);
                    }
                }
                if (ImageViewImage.Pixbuf != null)
                {
                    // TODO: How to get gdk-image from gtk-image?
                    //image = BrightenImage(
                    //    image, Configuration.Global.ImageBrightnessFactor);
                    //ImageViewImage.Pixbuf.GetFromImage(image, image.Colormap, 
                    //    0, 0, 0, 0, image.Width, image.Height);
                    ImageViewImage.Pixbuf = ImageViewImage.Pixbuf.ScaleSimple(
                        (int) Math.Round(
                            ImageViewImage.Pixbuf.Width * scaleFactor),
                        (int) Math.Round(
                            ImageViewImage.Pixbuf.Height * scaleFactor),
                        InterpType.Bilinear);
                    // very important
                    System.GC.Collect();
                    ActivateImageView();
                }
                else if ( ! isJpegNotSupported)
                {
                    DeactivateImageView();
                    MessageDialog(MessageType.Error,
                        string.Format("Unable to load image {0}/{1}.",
                            CorrectIndex ? imageIndex : imageIndex + 1, 
                            CorrectIndex ? images.Length - 1 : images.Length));
                }
            }
            catch (Exception e)
            {
                DeactivateImageView();
                ExceptionDialog d = new ExceptionDialog(
                    string.Format("Problems processing image {0}/{1}:\n{2}",
                        CorrectIndex ? imageIndex : imageIndex + 1, 
                        CorrectIndex ? images.Length - 1 : images.Length,
                        e.Message),
                    e);
                d.Self.Run();
            }
        }
    }

    private TreeIter AppendNode(TreeStore store, DataElement element)
    {
        TreeIter node;
        if (element.Value.IsMultiValue)
        {
            node = store.AppendValues(
                element.Tag.ToString(),
                element.VR.Tag.GetDictionaryEntry().Description,
                element.Value.ToString(),
                element.VR.ToLongString(),
                element.VR.Tag.GetDictionaryEntry().VM.ToString(),
                element.Value.ValueLength.ToString(),
                element.StreamPosition.ToString());
            foreach (object o in element.Value)
            {
                store.AppendValues(
                    node,
                    "",
                    "",
                    o.ToString(),
                    "",
                    "",
                    "",
                    "");

            }
        }
        else if (element.Value.IsDate)
        {
            node = store.AppendValues(
                element.Tag.ToString(),
                element.VR.Tag.GetDictionaryEntry().Description,
                ((DateTime) element.Value[0]).ToShortDateString(),
                element.VR.ToLongString(),
                element.VR.Tag.GetDictionaryEntry().VM.ToString(),
                element.Value.ValueLength.ToString(),
                element.StreamPosition.ToString());
        }
        else
        {
            node = store.AppendValues(
                element.Tag.ToString(),
                element.VR.Tag.GetDictionaryEntry().Description,
                element.Value.IsEmpty ? "" : element.Value[0].ToString(),
                element.VR.ToLongString(),
                element.VR.Tag.GetDictionaryEntry().VM.ToString(),
                element.Value.ValueLength.ToString(),
                element.StreamPosition.ToString());
        }
        return node;
    }

    private void AppendAllSubnodes(TreeIter parentNode, TreeStore store,
        DataElement element)
    {
        if (element.Value.IsSequence)
        {
            foreach (DataElement d in (Sequence) element.Value[0])
            {
                TreeIter node = store.AppendValues(
                    parentNode,
                    d.Tag.ToString(),
                    d.VR.Tag.GetDictionaryEntry().Description,
                    d.Value.IsEmpty ? "" : d.Value[0].ToString(),
                    d.VR.ToLongString(),
                    d.VR.Tag.GetDictionaryEntry().VM.ToString(),
                    d.Value.ValueLength.ToString(),
                    d.StreamPosition.ToString());
                AppendAllSubnodes(node, store, d);
            }
        }
        else if (element.Value.IsNestedDataSet)
        {
            foreach (DataElement d in (NestedDataSet) element.Value[0])
            {
                TreeIter node = store.AppendValues(
                    parentNode,
                    d.Tag.ToString(),
                    d.VR.Tag.GetDictionaryEntry().Description,
                    d.Value.IsEmpty ? "" : d.Value[0].ToString(),
                    d.VR.ToLongString(),
                    d.VR.Tag.GetDictionaryEntry().VM.ToString(),
                    d.Value.ValueLength.ToString(),
                    d.StreamPosition.ToString());
                AppendAllSubnodes(node, store, d);
            }
        }
    }

    private void RefreshDicomTreeView()
    {
        if (DicomFile != null)
        {
            TreeStore store = new TreeStore(typeof(string), typeof(string), 
               typeof(string), typeof(string), typeof(string),
               typeof(string), typeof(string));
            MainTreeView.Model = store;
            try
            {
                foreach (DataElement d in DicomFile.GetJointDataSets())
                {
                    TreeIter node = AppendNode(store, d);
                    AppendAllSubnodes(node, store, d);
                }
            }
            catch (Exception e)
            {
                ExceptionDialog d = new ExceptionDialog(
                    "Unexpected problems refreshing data sets view.", e);
                d.Self.Run();
            }
        }
    }

    private void RefreshFileInfoTreeView(string fileName)
    {
        if (DicomFile != null)
        {
            TreeStore store = new TreeStore(typeof(string), typeof(string));
            MainFileInfoTreeView.Model = store;
            try
            {
                bool isDicomFile = 
                    openDicom.File.DicomFile.IsDicomFile(fileName);
                store.AppendValues("FilePath", fileName);
                store.AppendValues("FileType", 
                    isDicomFile ? "DICOM 3.0" : "ACR-NEMA 1.0 or 2.0");
                Tag modalityTag = new Tag("0008", "0060");
                store.AppendValues("FileModality",
                    DicomFile.DataSet.Contains(modalityTag) ? 
                        DicomFile.DataSet[modalityTag].Value[0].ToString() : 
                        "(not defined)");
                store.AppendValues("FilePreamble",
                    isDicomFile ? 
                        ((DicomFile) DicomFile).MetaInformation.FilePreamble :
                        "(not defined)");
                store.AppendValues("HasPixelData",
                    DicomFile.HasPixelData ? "Yes" : "No");
                store.AppendValues("ImageResolution",
                    DicomFile.HasPixelData ? 
                        DicomFile.PixelData.Columns.ToString() + "x" + 
                        DicomFile.PixelData.Rows.ToString()  + "x" + 
                        (DicomFile.PixelData.SamplesPerPixel * 
                            DicomFile.PixelData.BitsStored).ToString() : 
                        "(not defined)");
                Tag numberOfFramesTag = new Tag("0028", "0008");
                store.AppendValues("NumberOfFrames",
                    DicomFile.DataSet.Contains(numberOfFramesTag) ?
                        DicomFile.DataSet[numberOfFramesTag].Value[0]
                            .ToString() : "(not defined)");
                store.AppendValues("CharacterEncoding",
                    DicomFile.DataSet.TransferSyntax.CharacterRepertoire
                        .Encoding.WebName.ToUpper());
                store.AppendValues("TransferSyntax",
                    DicomFile.DataSet.TransferSyntax.Uid.GetDictionaryEntry()
                        .Name);
                store.AppendValues("ValueRepresentation",
                    DicomFile.DataSet.TransferSyntax.IsImplicitVR ? 
                        "Implicit" : "Explicit");
                store.AppendValues("FileByteOrdering",
                    DicomFile.DataSet.TransferSyntax.IsLittleEndian ? 
                        "Little Endian" : "Big Endian");
                store.AppendValues("MachineByteOrdering",
                    DicomFile.DataSet.TransferSyntax.IsMachineLittleEndian ? 
                        "Little Endian" : "Big Endian");
            }
            catch (Exception e)
            {
                ExceptionDialog d = new ExceptionDialog(
                    "Unexpected problems refreshing file info view.", e);
                d.Self.Run();
            }
        }
    }
    
    private void MessageDialog(MessageType messageType, string message)
    {
        MessageDialog m = new MessageDialog(
                    Self,      
                    DialogFlags.Modal, 
                    messageType, 
                    ButtonsType.Ok,
                    message);
        m.Run();
        m.Destroy();
    }

    private void OnExportAsMenuItemActivate(object o, EventArgs args) 
    {
        ExportAsFileChooserDialog exportAsFileChooserDialog = 
            new ExportAsFileChooserDialog();
        int response = exportAsFileChooserDialog.Self.Run();
        if (response == -5)
        {
            string xmlFileName = exportAsFileChooserDialog.FileName;
            try
            {
                XmlFile xmlFile = new XmlFile(DicomFile, 
                    exportAsFileChooserDialog.ExcludePixelData);
                xmlFile.SaveTo(xmlFileName);
            }
            catch (Exception e)
            {
                ExceptionDialog d = new ExceptionDialog(
                  "Unexpected problems exporting as xml.", e);
                d.Self.Run();
            }
        }
    }

    private void OnPreferencesMenuItemActivate(object o, EventArgs args) 
    {
        new PreferencesDialog();
    }

    private void OnOpenToolButtonClicked(object o, EventArgs args)
    {
        OnOpenMenuItemActivate(o, args);
    }

    private void OnSaveImageToolButtonClicked(object o, EventArgs args) 
    {
        OnSaveImageMenuItemActivate(o, args);
    }

    private void OnExportAsToolButtonClicked(object o, EventArgs args) 
    {
        OnExportAsMenuItemActivate(o, args);
    }

    private void OnPreferencesToolButtonClicked(object o, EventArgs args) 
    {
        OnPreferencesMenuItemActivate(o, args);
    }

    private void OnQuitToolButtonClicked(object o, EventArgs args) 
    {
        OnQuitMenuItemActivate(o, args);
    }

    private void OnFirstSlideToolButtonClicked(object o, EventArgs args)
    {
        imageIndex = 0;
        ShowImage();
    }

    private void OnLastSlideToolButtonClicked(object o, EventArgs args)
    {
        imageIndex = images.Length - 1;
        ShowImage();
    }

    private void OnNextSlideToolButtonClicked(object o, EventArgs args)
    {
        imageIndex++;
        ShowImage();
    }

    private void OnPrevSlideToolButtonClicked(object o, EventArgs args)
    {
        imageIndex--;
        ShowImage();
    }

    private void OnCycleSlidesToolButtonClicked(object o, EventArgs args)
    {
        if ( ! isSlideCycling)
        {
            OpenMenuItem.Sensitive = false;
            SaveImageMenuItem.Sensitive = false;
            ExportAsMenuItem.Sensitive = false;
            PreferencesMenuItem.Sensitive = false;
            OpenToolButton.Sensitive = false;
            SaveImageToolButton.Sensitive = false;
            ExportAsToolButton.Sensitive = false;
            PreferencesToolButton.Sensitive = false;
            FirstSlideToolButton.Sensitive = false;
            PrevSlideToolButton.Sensitive = false;
            NextSlideToolButton.Sensitive = false;
            LastSlideToolButton.Sensitive = false;
            //BrightenToolButton.Sensitive = false;
            //DarkenToolButton.Sensitive = false;
            GimpToolButton.Sensitive = false;
            CycleSlidesToolButton.StockId = "gtk-media-stop";
            slideCyclingIdleHandler = 
                Idle.Add(new IdleHandler(CycleSlides));
            isSlideCycling = ! isSlideCycling;
        }
        else
        {
            GLib.Source.Remove(slideCyclingIdleHandler);
            OpenMenuItem.Sensitive = true;
            SaveImageMenuItem.Sensitive = true;
            ExportAsMenuItem.Sensitive = true;
            PreferencesMenuItem.Sensitive = true;
            OpenToolButton.Sensitive = true;
            SaveImageToolButton.Sensitive = true;
            ExportAsToolButton.Sensitive = true;
            PreferencesToolButton.Sensitive = true;
            FirstSlideToolButton.Sensitive = true;
            PrevSlideToolButton.Sensitive = true;
            NextSlideToolButton.Sensitive = true;
            LastSlideToolButton.Sensitive = true;
            //BrightenToolButton.Sensitive = true;
            //DarkenToolButton.Sensitive = true;
            GimpToolButton.Sensitive = true;
            CycleSlidesToolButton.StockId = "gtk-media-play";
            isSlideCycling = ! isSlideCycling;
        }
    }

    private bool CycleSlides()
    {
        imageIndex++;
        if (imageIndex >= images.Length)
            imageIndex = 0;
        ShowImage();
        System.Threading.Thread.Sleep((int) CyclingSpeedHScale.Value);
        // keeps running the idle routine
        return true;
    }

    private void OnZoomInToolButtonClicked(object o, EventArgs args)
    {
        scaleFactor += scaleStep;
        ShowImage();
    }

    private void OnZoomOutToolButtonClicked(object o, EventArgs args)
    {
        scaleFactor -= scaleStep;
        ShowImage();
    }

    private void OnOriginalSizeToolButtonClicked(object o, EventArgs args)
    {
        scaleFactor = 1.0;
        ShowImage();
    }

    private void OnFitWindowToolButtonClicked(object o, EventArgs args)
    {
        if (DicomFile.HasPixelData)
        {
            double dx = ImageViewViewport.Hadjustment.PageSize - 
                DicomFile.PixelData.Columns;
            double dy =  ImageViewViewport.Vadjustment.PageSize - 
                DicomFile.PixelData.Rows;
            if (dx <= dy)
            {
                scaleFactor = ImageViewViewport.Hadjustment.PageSize / 
                    (double) DicomFile.PixelData.Columns;
            }
            else
            {
                scaleFactor = ImageViewViewport.Vadjustment.PageSize / 
                    (double) DicomFile.PixelData.Rows;
            }
            ShowImage();
        }
    }

    private void OnBrightenToolButtonClicked(object o, EventArgs args)
    {
        Configuration.Global.ImageBrightnessFactor += brightnessStep;
        ShowImage();
    }

    private void OnDarkenToolButtonClicked(object o, EventArgs args)
    {
        Configuration.Global.ImageBrightnessFactor -= brightnessStep;
        ShowImage();
    }

    private void OnGimpToolButtonClicked(object o, EventArgs args)
    {
        string gimpRemoteExecutable = Configuration.Global.GimpRemoteExecutable;
        bool isUnix = 
            Regex.IsMatch(Environment.OSVersion.ToString().ToLower(), "unix");
        if ( ! File.Exists(gimpRemoteExecutable) && isUnix)
        {
            gimpRemoteExecutable = "/usr/bin/gimp-remote";
            if (File.Exists(gimpRemoteExecutable))
                Configuration.Global.GimpRemoteExecutable = 
                    gimpRemoteExecutable;
        }
        if (File.Exists(Configuration.Global.GimpRemoteExecutable))
        {
            string tempFileName = Path.GetTempFileName();
            ImageViewImage.Pixbuf.Save(tempFileName, "png");
            try
            {
                Process.Start(Configuration.Global.GimpRemoteExecutable,
                    tempFileName);
            }
            catch (Exception e)
            {
                ExceptionDialog d = new ExceptionDialog(
                    "Unexpected exception executing GIMP. Please make sure " +
                    "the right GIMP remote executable has been selected at " +
                    "preferences, e.g. \"gimp-win-remote.exe\" on a Windows " +
                    "machine or \"gimp-remote\" on GNU/Linux.", e);
                d.Self.Run();
            }
        }
        else
            MessageDialog(MessageType.Info, "No GIMP remote executable is " +
                "registered. Please make sure you have installed GIMP and " +
                "correctly selected its remote executable at preferences, " +
                "e.g. \"gimp-win-remote.exe\" on a Windows machine or " +
                "\"gimp-remote\" on GNU/Linux.");
    }
}