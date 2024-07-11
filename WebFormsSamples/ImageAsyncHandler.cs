using System;
using System.Text;
using System.Threading;
using System.Web;
using System.Drawing;
using System.Drawing.Imaging;
using System.Configuration;
using System.IO;
using System.Collections.Specialized;

using VNWEB40_GeneralUtilities;
using VNWEB40_ImageProcessor_Components;

namespace VNWEB40_ImageProcessor {

public class ImageAsyncHandler : IHttpAsyncHandler {
  public bool IsReusable {
    get {
      return false;
    }
  }

  public ImageAsyncHandler() {
  }

  public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, Object extraData) {
    AsynchOperation objAsynch = new AsynchOperation(cb, context, extraData);

    //context.Response.Write("<p>Begin IsThreadPoolThread is " + Thread.CurrentThread.IsThreadPoolThread + "</p>\r\n");

    objAsynch.StartAsyncWork();

    return (objAsynch);
  }

  public void EndProcessRequest(IAsyncResult result) {
  }

  public void ProcessRequest(HttpContext context) {
    throw new InvalidOperationException();
  }
}

class AsynchOperation : IAsyncResult {
  private bool _bCompleted;
  private Object _objState;
  private AsyncCallback _objCallback;
  private HttpContext _objContext;

  private const int WIDTH_DEFAULT = 75;     //In case problem with web.config
  private const int HEIGHT_DEFAULT = 75;    //In case problem with web.config
  private const int CWIDTH_DEFAULT = 810;   //In case problem with web.config
  private const int CHEIGHT_DEFAULT = 525;  //In case problem with web.config
  private const int CWIDTH_MAX = 2560;      //In case problem with web.config
  private const int CHEIGHT_MAX = 1920;     //In case problem with web.config

  //private const int WIDTH_MAX = 100;		//In case problem with web.config
  //private const int HEIGHT_MAX = 100;		//In case problem with web.config
  //private const int WIDTH_MIN = 25;			//In case problem with web.config
  //private const int HEIGHT_MIN = 25;		//In case problem with web.config

  private const int THUMB_CUTOFF = 120;  //If h or w is larger than 120, redraw the image, rather than thumb

  private const int MAX_CROP_PCT = 80;  //Kind of arbitrary but try it

  //private const float MAX_ALLOWABLE_ASPECT = 1.333333333333333333333333F;

  private int _iCanvasWidth;  //Cliff 6/27/12: Added these
  private int _iCanvasHeight;

  private int _iWidth;
  private int _iHeight;
  private int _iWidthDefault = WIDTH_DEFAULT;
  private int _iHeightDefault = HEIGHT_DEFAULT;
  private int _iCWidthDefault = CWIDTH_DEFAULT;
  private int _iCHeightDefault = CHEIGHT_DEFAULT;
  private int _iCWidthMax = CWIDTH_DEFAULT;
  private int _iCHeightMax = CHEIGHT_DEFAULT;
  private string _sPhotoRootPath;
  //private string _sImagePath;
  //private string _sImageAbsPath; //Cliff 6/8/12: Added

  private bool _bRotate90 = false;
  private bool _bRotate180 = false;
  private bool _bRotate270 = false;
  private RotateFlipType m_eRotateFlip = RotateFlipType.RotateNoneFlipNone;  //derived from querystring

  //private int _iContainerHeight;
  //private int _iContainerWidth;

  //private bool _bDimsAsBounds = false;   //use specified w and h as bounds, not actual size of image
  //private bool _bScaleImage = true;     //if no w and h are specified, should default thumb dims be used, or show actual image
  private int _iScaleMode = 1;  //default is to use h and w as thumb dims for explicit resize

  private Bitmap _objResult = null;
  private Bitmap _objImageOrig = null;

  private bool _bUseCache = Caching.UseCache();

  private int _iX1 = 0;  //Offsets
  private int _iX2 = 0;
  private int _iY1 = 0;
  private int _iY2 = 0;

  private bool _bShowCroppedImage = false;

  private bool _bShowInverted = false;
  private bool _bShowGray = false;
  private int _iBrightness = 0;  //no brightness adjustment by default
  private int _iContrast = 0;

  private RGBType _objColorFilter = new RGBType(0, 0, 0);

  private string _sAbsPath = string.Empty;

#region Configuration

  private void GetConfigSettings() {
    //Determine defaults
    _iWidthDefault = (int) WebUtilities.SafeGetQuerystringParam(ConfigurationManager.AppSettings, "thumb_width_default", typeof(int), WIDTH_DEFAULT);
    _iHeightDefault = (int) WebUtilities.SafeGetQuerystringParam(ConfigurationManager.AppSettings, "thumb_Height_default", typeof(int), HEIGHT_DEFAULT);

    _iCWidthDefault = (int) WebUtilities.SafeGetQuerystringParam(ConfigurationManager.AppSettings, "thumb_cwidth_default", typeof(int), CWIDTH_DEFAULT);
    _iCHeightDefault = (int) WebUtilities.SafeGetQuerystringParam(ConfigurationManager.AppSettings, "thumb_cHeight_default", typeof(int), CHEIGHT_DEFAULT);

    _iCWidthMax = (int) WebUtilities.SafeGetQuerystringParam(ConfigurationManager.AppSettings, "thumb_cwidth_max", typeof(int), CWIDTH_MAX);
    _iCHeightMax = (int) WebUtilities.SafeGetQuerystringParam(ConfigurationManager.AppSettings, "thumb_cHeight_max", typeof(int), CHEIGHT_MAX);

    //Determine limits
    //int iWidthMax = (int) WebUtilities.SafeGetQuerystringParam(ConfigurationSettings.AppSettings, "thumb_width_max", typeof(int), WIDTH_MAX);
    //int iWidthMin = (int) WebUtilities.SafeGetQuerystringParam(ConfigurationSettings.AppSettings, "thumb_width_min", typeof(int), WIDTH_MIN);
    //int iHeightMax = (int) WebUtilities.SafeGetQuerystringParam(ConfigurationSettings.AppSettings, "thumb_height_max", typeof(int), HEIGHT_MAX);
    //int iHeightMin = (int) WebUtilities.SafeGetQuerystringParam(ConfigurationSettings.AppSettings, "thumb_height_min", typeof(int), HEIGHT_MIN);

    _sPhotoRootPath = WebUtilities.SafeGetQuerystringParam(ConfigurationManager.AppSettings, "photo_path_root", typeof(string), "").ToString();
  }

  private void GetParams(NameValueCollection collQueryString) {
    _iCanvasWidth = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "cw", typeof(int), 0);
    _iCanvasHeight = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "ch", typeof(int), 0);

    //Now get actual thumb width and height - first try querystring
    _iWidth = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "iw", typeof(int), 0);
    _iHeight = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "ih", typeof(int), 0);

    //_iWidth = Convert.ToInt32(collQueryString["iw"]);
    //_objContext.Response.Write(collQueryString.ToString());
    //_objContext.Response.Write(collQueryString["iw"]);
    //_objContext.Response.Write(_iWidth);
    //_objContext.Response.Write(_iHeight);
    //_objContext.Response.Write(string.Format("WIDTH: {0}", _iWidth));

    _iScaleMode = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "sm", typeof(int), 1);

    //Paths not needed for handler

    //_sImagePath = objServer.UrlDecode(WebUtilities.SafeGetQuerystringParam(objRequest.QueryString, "path", typeof(string), "").ToString());

    //Cliff 6/8/12: Added
    //_sImageAbsPath = objServer.UrlDecode(WebUtilities.SafeGetQuerystringParam(objRequest.QueryString, "abspath", typeof(string), "").ToString());

    _bRotate90 = (bool) WebUtilities.SafeGetQuerystringParam(collQueryString, "r90", typeof(bool), false);
    _bRotate180 = (bool) WebUtilities.SafeGetQuerystringParam(collQueryString, "r180", typeof(bool), false);
    _bRotate270 = (bool) WebUtilities.SafeGetQuerystringParam(collQueryString, "r270", typeof(bool), false);

    _iX1 = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "x1", typeof(int), 0);
    _iX2 = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "x2", typeof(int), 0);
    _iY1 = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "y1", typeof(int), 0);
    _iY2 = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "y2", typeof(int), 0);

    _bShowCroppedImage = (bool) WebUtilities.SafeGetQuerystringParam(collQueryString, "sc", typeof(bool), false);

    _bShowInverted = (bool) WebUtilities.SafeGetQuerystringParam(collQueryString, "iv", typeof(bool), false);
    _bShowGray = (bool) WebUtilities.SafeGetQuerystringParam(collQueryString, "gr", typeof(bool), false);
    _iBrightness = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "br", typeof(int), 0);
    _iContrast = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "ct", typeof(int), 0);

    _objColorFilter.Red = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "cfr", typeof(int), 0);
    _objColorFilter.Blue = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "cfb", typeof(int), 0);
    _objColorFilter.Green = (int) WebUtilities.SafeGetQuerystringParam(collQueryString, "cfg", typeof(int), 0);

    //_bDimsAsBounds = (bool)WebUtilities.SafeGetQuerystringParam(objRequest.QueryString, "bounds", typeof(bool), false);
    //_iContainerWidth = (int)WebUtilities.SafeGetQuerystringParam(objRequest.QueryString, "cwidth", typeof(int), 0);    //Screen width, e.g. 1024 - UI element widths
    //_iContainerHeight = (int)WebUtilities.SafeGetQuerystringParam(objRequest.QueryString, "cheight", typeof(int), 0);
  }

#endregion

#region ImageProcessing

  private float GetMaxAspectRatio() {
    return (4F / 3F);
  }

  public bool ThumbnailCallback() {
    return false;
  }

  private void ProcessParams() {
    //Make determinations about dimensions to use and other adjustments.

    if (_iBrightness < -100)  //Max and Min check
      _iBrightness = -100;
    else if (_iBrightness > 100)
      _iBrightness = 100;

    if (_iContrast < -100)  //Max and Min check
      _iContrast = -100;
    else if (_iContrast > 100)
      _iContrast = 100;

    if (_objColorFilter.Red < -255)  //Max and Min check
      _objColorFilter.Red = -255;
    else if (_objColorFilter.Red > 255)
      _objColorFilter.Red = 255;

    if (_objColorFilter.Green < -255)  //Max and Min check
      _objColorFilter.Green = -255;
    else if (_objColorFilter.Green > 255)
      _objColorFilter.Green = 255;

    if (_objColorFilter.Blue < -255)  //Max and Min check
      _objColorFilter.Blue = -255;
    else if (_objColorFilter.Blue > 255)
      _objColorFilter.Blue = 255;

    if (_iX1 < 0 || _iX1 > MAX_CROP_PCT)  //Do other validation on cropping offsets?  Compare to _iWidth, etc.
      _iX1 = 0;

    if (_iX2 < 0 || _iX2 > MAX_CROP_PCT)
      _iX2 = 0;

    if (_iY1 < 0 || _iY1 > MAX_CROP_PCT)
      _iY1 = 0;

    if (_iY2 < 0 || _iY2 > MAX_CROP_PCT)
      _iY2 = 0;

    int iUseWidth = 0;
    int iUseHeight = 0;

    if (_bRotate90)  //Only want one of these rotate vals to be possibly true
    {
      _bRotate270 = false;
      _bRotate180 = false;

      m_eRotateFlip = RotateFlipType.Rotate90FlipNone;

    } else if (_bRotate180) {
      _bRotate270 = false;
      _bRotate90 = false;

      m_eRotateFlip = RotateFlipType.Rotate180FlipNone;

    } else if (_bRotate270) {
      _bRotate90 = false;
      _bRotate180 = false;

      m_eRotateFlip = RotateFlipType.Rotate270FlipNone;

    } else  //redundant
    {
      _bRotate90 = false;
      _bRotate180 = false;
      _bRotate270 = false;

      m_eRotateFlip = RotateFlipType.RotateNoneFlipNone;
    }

    if (_bRotate90 || _bRotate270) {
      iUseWidth = _objImageOrig.Height;  //Reverse the dims
      iUseHeight = _objImageOrig.Width;

      //Support.SwapInt(ref _iCWidthDefault, ref _iCHeightDefault);
      //Support.SwapInt(ref _iCWidthMax, ref _iCHeightMax);
      //Support.SwapInt(ref _iWidth, ref _iHeight);

    } else if (_bRotate180) {
      iUseWidth = _objImageOrig.Width;
      iUseHeight = _objImageOrig.Height;

    } else {
      iUseWidth = _objImageOrig.Width;
      iUseHeight = _objImageOrig.Height;
    }

    //Now proceed with checking dims

    //Cliff 6/27/12: Added scale mode 5 which will center resized image on a canvas

    if (_iScaleMode == 2 || _iScaleMode == 5)  //explicit resize to fit in container h x w
    {
      int iMinWidth = Math.Min(iUseWidth, _iCWidthDefault);
      int iMaxWidth = Math.Min(iUseWidth, _iCWidthMax);

      if (_iWidth <= 0)
        _iWidth = iMinWidth;
      else if (_iWidth > iMaxWidth)
        _iWidth = iMaxWidth;

      int iMinHeight = Math.Min(iUseHeight, _iCHeightDefault);
      int iMaxHeight = Math.Min(iUseHeight, _iCHeightMax);

      if (_iHeight <= 0)
        _iHeight = iMinHeight;
      else if (_iHeight > iMaxHeight)
        _iHeight = iMaxHeight;

      //now determine ration to adjust dims by so image fits in container

      float fHeightRatio = (float) _iHeight / (float) iUseHeight;
      float fWidthRatio = (float) _iWidth / (float) iUseWidth;

      float fUseRatio = Math.Min(fHeightRatio, fWidthRatio);

      _iHeight = (int)(fUseRatio * (float) iUseHeight);
      _iWidth = (int)(fUseRatio * (float) iUseWidth);

    } else if (_iScaleMode == 3)  //force resized image to have specific height - let width flow according to aspect ratio.  But it must fit in a specified max.
    {
      int iMinWidth = Math.Min(iUseWidth, _iCWidthDefault);
      int iMaxWidth = Math.Min(iUseWidth, _iCWidthMax);

      if (_iWidth <= 0)
        _iWidth = iMinWidth;
      else if (_iWidth > iMaxWidth)
        _iWidth = iMaxWidth;

      int iMinHeight = Math.Min(iUseHeight, _iCHeightDefault);
      int iMaxHeight = Math.Min(iUseHeight, _iCHeightMax);

      if (_iHeight <= 0)
        _iHeight = iMinHeight;
      else if (_iHeight > iMaxHeight)
        _iHeight = iMaxHeight;

      float fDimRatio = (float) iUseWidth / (float) iUseHeight;

      if (fDimRatio <= GetMaxAspectRatio())
        _iWidth = (int)(fDimRatio * (float) _iHeight);
      else  //Prohibit aspect ratios higher than 1.33 in this mode!!  Else it will throw off the horizontal.
        _iWidth = (int)(GetMaxAspectRatio() * (float) _iHeight);

      //Basically, this mode ignores the width param passed in at the querystring.

      //Test this with rotation.

    } else if (_iScaleMode == 1)  //force resized image to have specific width - let height flow according to aspect ratio.  But it must fit in a specified max.
    {
      int iMinHeight = Math.Min(iUseHeight, _iCHeightDefault);
      int iMaxHeight = Math.Min(iUseHeight, _iCHeightMax);

      if (_iHeight <= 0)
        _iHeight = iMinHeight;
      else if (_iHeight > iMaxHeight)
        _iHeight = iMaxHeight;

      int iMinWidth = Math.Min(iUseWidth, _iCWidthDefault);
      int iMaxWidth = Math.Min(iUseWidth, _iCWidthMax);

      if (_iWidth <= 0)
        _iWidth = iMinWidth;
      else if (_iWidth > iMaxWidth)
        _iWidth = iMaxWidth;

      float fDimRatio = (float) iUseWidth / (float) iUseHeight;

      if (fDimRatio <= GetMaxAspectRatio())
        _iWidth = (int)(fDimRatio * (float) _iHeight);
      else  //Prohibit aspect ratios higher than 1.33 in this mode!!  Else it will throw off the horizontal.
        _iWidth = (int)(GetMaxAspectRatio() * (float) _iHeight);

      //Basically, this mode ignores the height param passed in at the querystring.

      //Test this with rotation.

    } else if (_iScaleMode == 4)  //explicit resize to h x w
    {
      if (_iWidth <= 0)
        _iWidth = _iWidthDefault;
      else if (_iWidth > iUseWidth)
        _iWidth = iUseWidth;

      if (_iHeight <= 0)
        _iHeight = _iHeightDefault;
      else if (_iHeight > iUseHeight)
        _iHeight = iUseHeight;

    } else if (_iScaleMode == 0)  //no resizing to be done
    {
      _iHeight = iUseHeight;  //regardless whether h and w were specified in querystring
      _iWidth = iUseWidth;
    }
  }

  private void GenerateImage() {
    bool bNotInCache = true;
    string sCacheKey = string.Empty;

    if (_bUseCache) {
      //if (!bUseActualImage)
      sCacheKey = ConstructCacheKey_Local(CacheItemType.IMAGE, _sAbsPath);  //, _iWidth, _iHeight);

      //_objContext.Response.Write(sCacheKey);
      //_objContext.Response.End();

      //else
      //  sCacheKey = ConstructCacheKey_Local(CacheItemType.IMAGE_ACTUAL, sAbsPath, _iWidth, _iHeight);

      //See if item is in Cache
      object objThing = Caching.GetFromCache(sCacheKey, _objContext.Cache);
      //Image objTemp = (Image)Caching.GetFromCache(sCacheKey, this);

      if (objThing != null)  //was in the cache
      {
        bNotInCache = false;

        _objResult = (Bitmap) objThing;

      } else
        bNotInCache = true;
    }

    if (!_bUseCache || bNotInCache) {
      //if (_objImageOrig == null)
      //_objImageOrig = Image.FromFile(sAbsPath);

      //Apply effects first
      if (m_eRotateFlip != RotateFlipType.RotateNoneFlipNone)
        _objImageOrig.RotateFlip(m_eRotateFlip);

      if (_bShowInverted)
        ImageFilters.Invert(_objImageOrig);

      if (_bShowGray)
        ImageFilters.GrayScale(_objImageOrig);

      if (_iBrightness != 0)
        ImageFilters.Brightness(_objImageOrig, (int)((255F / 100F) * _iBrightness));

      if (_iContrast != 0)
        ImageFilters.Contrast(_objImageOrig, (sbyte) _iContrast);

      if (_objColorFilter.Red != 0 || _objColorFilter.Green != 0 || _objColorFilter.Blue != 0)
        ImageFilters.Color(_objImageOrig, _objColorFilter.Red, _objColorFilter.Green, _objColorFilter.Blue);

      if (_iScaleMode != 0)  //resize
      {
        if (_iX1 > 0 || _iX2 > 0 || _iY1 > 0 || _iY2 > 0)  //Handle cropping separately
        {
          //if (m_eRotateFlip != RotateFlipType.RotateNoneFlipNone)
          //{
          //    _objImageOrig.RotateFlip(m_eRotateFlip);

          //}

          int iSrcX1 = 0;
          int iSrcX2 = 0;
          int iSrcY1 = 0;
          int iSrcY2 = 0;

          int iDestX1 = 0;
          int iDestX2 = 0;
          int iDestY1 = 0;
          int iDestY2 = 0;

          float fPct = 0F;

          int iWidthOrig = _iWidth;
          int iHeightOrig = _iHeight;

          //vars aren't dims, but offsets in pct.
          if (_iX1 > 0) {
            fPct = (float) _iX1 / 100F;
            iDestX1 = (int)(fPct * (float) iWidthOrig);
            _iWidth -= iDestX1;

            iSrcX1 = (int)(fPct * (float) _objImageOrig.Width);
          }

          if (_iX2 > 0) {
            fPct = (float) _iX2 / 100F;
            iDestX2 = (int)(fPct * (float) iWidthOrig);
            _iWidth -= iDestX2;

            iSrcX2 = (int)(fPct * (float) _objImageOrig.Width);
          }

          if (_iY1 > 0) {
            fPct = (float) _iY1 / 100F;
            iDestY1 = (int)(fPct * (float) iHeightOrig);
            _iHeight -= iDestY1;

            iSrcY1 = (int)(fPct * (float) _objImageOrig.Height);
          }

          if (_iY2 > 0) {
            fPct = (float) _iY2 / 100F;
            iDestY2 = (int)(fPct * (float) iHeightOrig);
            _iHeight -= iDestY2;

            iSrcY2 = (int)(fPct * (float) _objImageOrig.Height);
          }

          //objGraphics.Clear(System.Drawing.Color.White);    //Misc.

          Rectangle objDestRect = new Rectangle(0, 0, _iWidth, _iHeight);
          Rectangle objSrcRect = new Rectangle(iSrcX1, iSrcY1, _objImageOrig.Width - (iSrcX1 + iSrcX2), _objImageOrig.Height - (iSrcY1 + iSrcY2));

          if (_bShowCroppedImage) {
            _objResult = new Bitmap(_iWidth, _iHeight);  //Use this line to show the cropped image
            _objResult.SetResolution(_objImageOrig.HorizontalResolution, _objImageOrig.VerticalResolution);

            Graphics objGraphics = Graphics.FromImage(_objResult);
            objGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            objGraphics.DrawImage(_objImageOrig, objDestRect, objSrcRect, GraphicsUnit.Pixel);  //Use to draw the cropped image

          } else {
            _objResult = new Bitmap(iWidthOrig, iHeightOrig);
            _objResult.SetResolution(_objImageOrig.HorizontalResolution, _objImageOrig.VerticalResolution);

            //But try to draw rectangle instead as visual aid.

            Graphics objGraphics = Graphics.FromImage(_objResult);
            objGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            objGraphics.DrawImage(_objImageOrig, 0, 0, iWidthOrig, iHeightOrig);

            Pen objPen = new Pen(Color.Red, 1);

            if (_iX1 > 0)
              objGraphics.DrawLine(objPen, iDestX1, iDestY1, iDestX1, iDestY1 + _iHeight);

            if (_iX2 > 0)
              objGraphics.DrawLine(objPen, iDestX1 + _iWidth, iDestY1, iDestX1 + _iWidth, iDestY1 + _iHeight);

            if (_iY1 > 0)
              objGraphics.DrawLine(objPen, iDestX1, iDestY1, iDestX1 + _iWidth, iDestY1);

            if (_iY2 > 0)
              objGraphics.DrawLine(objPen, iDestX1, iDestY1 + _iHeight, iDestX1 + _iWidth, iDestY1 + _iHeight);
          }

        } else if (_iWidth > THUMB_CUTOFF || _iHeight > THUMB_CUTOFF) {
          //REVISIT: Don't use Thumbnails

          _objResult = new Bitmap(_iWidth, _iHeight);

          _objResult.SetResolution(_objImageOrig.HorizontalResolution, _objImageOrig.VerticalResolution);  //1/17/07: Added

          //if (m_eRotateFlip != RotateFlipType.RotateNoneFlipNone)
          //{
          //    _objImageOrig.RotateFlip(m_eRotateFlip);

          //}

          Graphics objGraphics = Graphics.FromImage(_objResult);
          objGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;  //1/17/07: Added

          //objGraphics.FillRectangle(Brushes.White, 0, 0, _iWidth, _iHeight);
          objGraphics.DrawImage(_objImageOrig, 0, 0, _iWidth, _iHeight);

        } else {  //Thumbnail

          Image.GetThumbnailImageAbort objCallback = new Image.GetThumbnailImageAbort(ThumbnailCallback);  //AddressOf

          _objResult = (Bitmap) _objImageOrig.GetThumbnailImage(_iWidth, _iHeight, objCallback, IntPtr.Zero);
        }

      } else {
        if (m_eRotateFlip != RotateFlipType.RotateNoneFlipNone) {
          _objImageOrig.RotateFlip(m_eRotateFlip);
        }

        _objResult = (Bitmap) _objImageOrig.Clone();  //REVISIT

        //TESTING
        // _objResult.RotateFlip(RotateFlipType.Rotate90FlipNone);        //REVISIT
      }

      ////TEST CODE
      //_objResult = null;
      //_objResult = (Bitmap)Caching.GetFromCache(sCacheKey, this);
      ////TEST CODE

      //Image objTemp = (Image)Caching.GetFromCache(sCacheKey, this);

      //_objResult = null;  //imgCanvas;

      if (_iScaleMode == 5 && _iCanvasWidth > _objResult.Width && _iCanvasHeight > _objResult.Height)  //new - center in canvas
      {
        Bitmap imgCanvas = new Bitmap(_iCanvasWidth, _iCanvasHeight);
        Graphics objGraphics = Graphics.FromImage(imgCanvas);
        objGraphics.FillRectangle(Brushes.White, 0, 0, _iCanvasWidth, _iCanvasHeight);

        //draw in center of canvas
        objGraphics.DrawImage(_objResult,
                              (_iCanvasWidth - _objResult.Width) / 2,
                              (_iCanvasHeight - _objResult.Height) / 2,
                              _objResult.Width,
                              _objResult.Height);

        _objResult = imgCanvas;
      }

      if (_bUseCache)
        Caching.AddToCache(CacheItemType.IMAGE, sCacheKey, _objResult, _objContext.Cache);
    }

    //Handle rotation: Recall...always rotating original,not maintaining state.
    //if (m_eRotateFlip != RotateFlipType.RotateNoneFlipNone)
    //{
    //    _objResult.RotateFlip(m_eRotateFlip);

    //}

    //if (bNotInCache)
    //{
    //    //FileStream fs = File.OpenRead(sAbsPath);
    //    //byte[] arrByte = new byte[(int)fs.Length];
    //    //fs.Read(arrByte, 0, (int) (fs.Length));
    //    //MemoryStream objMemStream = new MemoryStream(arrByte);
    //    //objBitmap = new Bitmap(objMemStream);

    //    if (objImage == null)   //Not loaded yet
    //        objImage = Image.FromFile(sAbsPath);

    //    //fs.Close();
    //    //fs = null;	//8/05 Added
    //    //arrByte = null;	//8/05 Added

    //    if (_bUseCache)	//Add to Cache
    //        Caching.AddToCache(CacheItemType.IMAGE_THUMB, sCacheKey, objImage, this);
    //}

    //Image.GetThumbnailImageAbort objCallback  = new Image.GetThumbnailImageAbort(ThumbnailCallback);	//AddressOf
    //_objThumb = objImage.GetThumbnailImage(_iWidth, _iHeight, objCallback, IntPtr.Zero);

    //if (Path.GetExtension(_sAbsPath).ToLower() == ".png")
    //{
    //    _objContext.Response.ContentType = "image/png"; //Needed?

    //    _objResult.Save(_objContext.Response.OutputStream, ImageFormat.Png);	//write as png to output stream

    //}
    //else
    //{
    _objContext.Response.ContentType = "image/jpeg";  //Needed?

    _objResult.Save(_objContext.Response.OutputStream, ImageFormat.Jpeg);  //write as jpeg to output stream

    // }
  }

#endregion

#region CacheHelper

  private string ConstructCacheKey_Local(CacheItemType eItemType, string sAbsPath) {
    //Make sure this is unique

    StringBuilder sResult = new StringBuilder(string.Empty);
    const string sDelim = "_";

    sResult.Append(string.Format("TYPE{0}{1}", eItemType.ToString(), sDelim));
    sResult.Append(string.Format("SM{0}{1}", _iScaleMode, sDelim));
    sResult.Append(string.Format("PATH{0}{1}", sAbsPath, sDelim));

    //_objContext.Response.Write(sResult.ToString());
    //_objContext.Response.End();

    sResult.Append(string.Format("W{0}{1}", _iWidth, sDelim));
    sResult.Append(string.Format("H{0}{1}", _iHeight, sDelim));
    sResult.Append(string.Format("R90{0}{1}", _bRotate90.ToString(), sDelim));
    sResult.Append(string.Format("R180{0}{1}", _bRotate180.ToString(), sDelim));
    sResult.Append(string.Format("R270{0}{1}", _bRotate270.ToString(), sDelim));
    sResult.Append(string.Format("X1{0}{1}", _iX1, sDelim));
    sResult.Append(string.Format("X2{0}{1}", _iX2, sDelim));
    sResult.Append(string.Format("Y1{0}{1}", _iY1, sDelim));
    sResult.Append(string.Format("Y2{0}{1}", _iY2, sDelim));
    sResult.Append(string.Format("SC{0}{1}", _bShowCroppedImage.ToString(), sDelim));
    sResult.Append(string.Format("IV{0}{1}", _bShowInverted.ToString(), sDelim));
    sResult.Append(string.Format("GR{0}{1}", _bShowGray.ToString(), sDelim));
    sResult.Append(string.Format("BR{0}{1}", _iBrightness, sDelim));
    sResult.Append(string.Format("CT{0}{1}", _iContrast, sDelim));
    sResult.Append(string.Format("CFR{0}{1}", _objColorFilter.Red, sDelim));
    sResult.Append(string.Format("CFG{0}{1}", _objColorFilter.Green, sDelim));
    sResult.Append(string.Format("CFB{0}{1}", _objColorFilter.Blue, sDelim));

    return sResult.ToString();
  }

#endregion

#region Properties

  bool IAsyncResult.IsCompleted {
    get {
      return _bCompleted;
    }
  }

  WaitHandle IAsyncResult.AsyncWaitHandle {
    get {
      return null;
    }
  }

  Object IAsyncResult.AsyncState {
    get {
      return _objState;
    }
  }

  bool IAsyncResult.CompletedSynchronously {
    get {
      return false;
    }
  }

#endregion

  public AsynchOperation(AsyncCallback callback, HttpContext context, Object state) {
    _objCallback = callback;
    _objContext = context;
    _objState = state;
    _bCompleted = false;
  }

  public void StartAsyncWork() {
    ThreadPool.QueueUserWorkItem(new WaitCallback(StartAsyncTask), null);
  }

  private void StartAsyncTask(Object workItemState) {
    //string sAbsPath = string.Empty;

    try {
      _sAbsPath = _objContext.Request.PhysicalPath;

      //sAbsPath = _objContext.Server.MapPath(sImageName);

      // if (!File.Exists(sAbsPath))
      //sAbsPath = _objContext.Server.MapPath("imagenotfound.jpg");
      //_objContext.Response.Redirect("ImageNotFound.jpeg");		//this will redirect to the image by this name in the same folder as the target image

      GetConfigSettings();  //web.config

      //process querystring
      GetParams(_objContext.Request.QueryString);

      if (!File.Exists(_sAbsPath))
        _sAbsPath = _objContext.Server.MapPath("imagenotfound.jpg");

      //if (_sImageAbsPath != string.Empty)
      //sAbsPath = _sImageAbsPath;
      //else if (_sImagePath != string.Empty)
      //sAbsPath = _objContext.Server.MapPath(_sImagePath);    //generating abs path from virtual path here
      //else
      //{
      //_objContext.Response.Write("ERROR: EMPTY PATH");
      //_objContext.Response.End();

      //}

      //if (_sImagePath == "" )
      //{
      //    Response.Write("ERROR: EMPTY PATH");
      //    Response.End();

      //}

      if (_objImageOrig == null)
        _objImageOrig = (Bitmap) Bitmap.FromFile(_sAbsPath);

      ProcessParams();

      //Image.GetThumbnailImageAbort objCallback = new Image.GetThumbnailImageAbort(ThumbnailCallback);	//AddressOf

      GenerateImage();

      if (_objImageOrig != null)  //cliff 9/20/12: Added block to avoid contention
      {
        _objImageOrig.Dispose();
        _objImageOrig = null;
      }

      if (_objResult != null)  //cliff 9/20/12: Added block to avoid contention
      {
        _objResult.Dispose();
        _objResult = null;
      }

      _bCompleted = true;

      _objCallback(this);

    } catch (System.Exception objEx) {
      //debugging code
      bool bMailResult = EmailUtilities.SMTPSendMail(
          ConfigurationManager.AppSettings["exceptionEmailFrom"],
          string.Format("{0}: VTNBS Image Processor Error", ConfigurationManager.AppSettings["envTag"]),
          string.Format("Message: {0}, Stack Trace: {1}", objEx.Message, objEx.StackTrace),
          ConfigurationManager.AppSettings["exceptionEmailTo"],  //for multiples - will be delimited in single string
          string.Empty,                                          //CC
          string.Empty,                                          //BCC
          false,
          ConfigurationManager.AppSettings["mailServer"],
          ConfigurationManager.AppSettings["exceptionEmailUser"],
          ConfigurationManager.AppSettings["exceptionEmailPwd"],
          string.Empty,
          string.Empty);

    } finally {
      if (_objImageOrig != null)  //cliff 9/20/12: Added block to avoid contention
      {
        _objImageOrig.Dispose();
        _objImageOrig = null;
      }

      if (_objResult != null)  //cliff 9/20/12: Added block to avoid contention
      {
        _objResult.Dispose();
        _objResult = null;
      }
    }
  }
}

}
