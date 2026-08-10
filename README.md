# DigitizePlot

https://github.com/litdev1/DigitizePlot/releases/download/Current/Setup.exe

## Introduction
I wanted a free Windows desktop plot digitizer that was quick and simple to use with a minimum of mouse clicks and pre/post processing to quickly get digital data from plots typically found in scientific papers.
## Workflow
### Open the program and load an image

* Paste (Ctrl V) an image directly over main plot region
* Drag and drop an image to the main plot area
* Press the Load image button to paste an image
* Use Open image to load an image from file

### Set up the axes
* The first 3 points selected with the mouse left click represent the origin (black), a point on the X-axis (green) and a point on the Y-axis (blue).  These points can also be repositioned with left mouse drag and drop.
* Set the coordinate values for Origin, X offset and Y offset in the boxes on the right panel.  It is assumed that the Y coordinate of the X-axis point is the same as the Origin and similarly for the Y offset.  The internal algorithm will account for skewness or rotations present in the image.
* Set the Data type to linear or logarithmic for each axis as required.
* Use axis Guides if required to line up axis points with origin if needed.


### Create digitized data
At any time the current model can be saved (Save data) and loaded (Load data), the saving includes all data points set in a simple xml format.  Additionally, the current data points (with or without the axis points) can be deleted with Clear all data and Clear points.

Decide if you want points to be sorted (by X value) as they are added – this is the usual default using the Sort checkbox.

Add data points (red) by left clicks.  Delete points with a second left click over a point, and move a point by holding shift with a left mouse click and drag.  Multiple deletions of data points can be made using Ctrl with left mouse button to select a ‘rubber- band’ region.

Export the digitized data to clipboard with Ctrl C over the main plot view, or using Export data.  The data (tab separated list or X Y values) can then be pasted directly in Excel, text document or other software.  The data in the app is displayed to 3 significant figures; greater accuracy is unrealistic digitizing an image.  However data is exported at maximum resolution.

## View options
The opacity of the main view can be altered to more clearly see the added points.
The magnification window scale can also be modified.  Note that the magnification may involve some minor rasterization effects.

Optional axis guides can be used to help align axes with origin.
## Automatic digitization
Turn this on or off with the Auto on checkbox.  Once it is on and a data point is selected, neighbour points with similar colour will also be detected and added.

Tolerance is a measure of colour difference between the original selected point and potential points on the same line.  Therefore care selecting a ‘good’ representative first point is required.  The tolerance is the difference in RMS of the two comparison points R,G,B values in byte form.

tol=√((〖∆R〗^2+〖∆G〗^2+〖∆B〗^2)/3)

Pixel step is the distance to search from one ‘found’ point to the next.  A larger value will be slower, and may pick up other similar colour points that are not adjacent.  However, it can help to ‘jump’ gaps for example lines crossing each other or even dashed lines.

Spacing is the X value spacing of the found points that are finally reported– they are always sorted by X value.  The spacing distance is pixel number on the main view image (which may be zoomed from the original image.
## Examples
Auto generated data on a log-log plot
<img width="910" height="762" alt="image" src="https://github.com/user-attachments/assets/aed64603-53d9-42e9-97fc-4e8e1bfde43c" />

Auto generated dashed line with axis guides to help align axes
<img width="910" height="762" alt="image" src="https://github.com/user-attachments/assets/f536ffe2-a282-4a6e-8848-689542b2c572" />

Auto generated wavy plot with thin data line
<img width="910" height="762" alt="image" src="https://github.com/user-attachments/assets/c6880ed5-5072-479a-84c8-566d6212b0e8" />
