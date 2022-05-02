using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfiumViewer;

namespace ConsoleApp
{
    class Program
    {
        
        static void Main(string[] args)
        {
            List<string> files = new List<string>();
            string inputPdf = @"C:\code\sample.pdf";
            string inputPdf2 = @"C:\code\sample2.pdf";
            string outputPng = @"C:\code\output.tiff";

            files.Add(inputPdf);
            files.Add(inputPdf2);
            
            RenderPdfToFile(files.ToArray(), outputPng, 300);

            //using (MagickImageCollection images = new MagickImageCollection())
            //{
            //    images.Read(inputPdf);
            //    using (IMagickImage vertical = images.AppendVertically())
            //    {
            //        vertical.Format = MagickFormat.Png;
            //        vertical.Density = new Density(300);
            //        vertical.Write(outputPng);
            //    }
            //}


        }
        static void RenderPdfToFile(string[] pdfFiles, string outputImageFilename, int dpi)
        {
            int counter = 1;
            foreach (var pdfFile in pdfFiles)
            {
                // Load PDF Document from file
                using (var doc = PdfDocument.Load(pdfFile))
                {
                    // Loop through pages
                    for (int page = 0; page < doc.PageCount; page++)
                    {                        
                        // Render with dpi and with forPrinting false
                        using (var img = doc.Render(page, dpi, dpi, false))
                        {   
                            img.Save($"C:\\code\\file{counter}.tiff", ImageFormat.Tiff);     // Save rendered image to disc
                        }
                        counter++;
                    }
                }
            }

            MergeTiffFiles(outputImageFilename);

        }

        private static void MergeTiffFiles(string outputImageFilename)
        {
            var allFiles = Directory.GetFiles(@"C:\code", "*.tiff");
            byte[][] filesData = new byte[allFiles.Length][];

            for (int i = 0; i < allFiles.Length; i++)
            {
                var fileData = File.ReadAllBytes($"c:\\code\\file{i + 1}.tiff");
                filesData[i] = fileData;
            }


            var targetFileData = TiffHelper.MergeTiff(filesData.ToArray());

            File.WriteAllBytes(outputImageFilename, targetFileData);
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo info = null;
            foreach (ImageCodecInfo ice in ImageCodecInfo.GetImageEncoders())
                if (ice.MimeType == mimeType)
                    info = ice;

            return info;
        }
    }


    public static class TiffHelper
    {
        /// <summary>
        /// Merges multiple TIFF files (including multipage TIFFs) into a single multipage TIFF file.
        /// </summary>
        public static byte[] MergeTiff(params byte[][] tiffFiles)
        {
            byte[] tiffMerge = null;
            using (var msMerge = new MemoryStream())
            {
                //get the codec for tiff files
                ImageCodecInfo ici = null;
                foreach (ImageCodecInfo i in ImageCodecInfo.GetImageEncoders())
                    if (i.MimeType == "image/tiff")
                        ici = i;

                Encoder enc = Encoder.SaveFlag;
                EncoderParameters ep = new EncoderParameters(1);

                Bitmap pages = null;
                int frame = 0;

                foreach (var tiffFile in tiffFiles)
                {
                    using (var imageStream = new MemoryStream(tiffFile))
                    {
                        using (Image tiffImage = Image.FromStream(imageStream))
                        {
                            foreach (Guid guid in tiffImage.FrameDimensionsList)
                            {
                                //create the frame dimension 
                                FrameDimension dimension = new FrameDimension(guid);
                                //Gets the total number of frames in the .tiff file 
                                int noOfPages = tiffImage.GetFrameCount(dimension);

                                for (int index = 0; index < noOfPages; index++)
                                {
                                    FrameDimension currentFrame = new FrameDimension(guid);
                                    tiffImage.SelectActiveFrame(currentFrame, index);
                                    using (MemoryStream tempImg = new MemoryStream())
                                    {
                                        tiffImage.Save(tempImg, ImageFormat.Tiff);
                                        {
                                            if (frame == 0)
                                            {
                                                //save the first frame
                                                pages = (Bitmap)Image.FromStream(tempImg);
                                                ep.Param[0] = new EncoderParameter(enc, (long)EncoderValue.MultiFrame);
                                                pages.Save(msMerge, ici, ep);
                                            }
                                            else
                                            {
                                                //save the intermediate frames
                                                ep.Param[0] = new EncoderParameter(enc, (long)EncoderValue.FrameDimensionPage);
                                                pages.SaveAdd((Bitmap)Image.FromStream(tempImg), ep);
                                            }
                                        }
                                        frame++;
                                    }
                                }
                            }
                        }
                    }
                }
                if (frame > 0)
                {
                    //flush and close.
                    ep.Param[0] = new EncoderParameter(enc, (long)EncoderValue.Flush);
                    pages.SaveAdd(ep);
                }

                msMerge.Position = 0;
                tiffMerge = msMerge.ToArray();
            }
            return tiffMerge;
        }
    }
}
