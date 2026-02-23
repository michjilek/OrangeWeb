/* Start Camera */

//window.startCamera = async function (videoElementId) {
//    const video = document.getElementById(videoElementId);
//    if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
//        try {
//            const stream = await navigator.mediaDevices.getUserMedia({ video: true });
//            video.srcObject = stream;
//            video.play();
//        } catch (err) {
//            console.error("Chyba při přístupu ke kameře: ", err);
//        }
//    } else {
//        console.error("Prohlížeč nepodporuje getUserMedia API.");
//    }
//};

///* Notify me, when Screen Size changed */
//let resizeTimeout;
//window.notifyScreenResize = () => {
//    if (resizeTimeout) clearTimeout(resizeTimeout);
//    resizeTimeout = setTimeout(() => {
//        const width = window.innerWidth;
//        const height = window.innerHeight;
//        DotNet.invokeMethodAsync('OP_Razor_Components_Library', 'OnScreenResize', width, height);
//    }, 200); // Add debounce for performance
//};

//window.addEventListener('resize', window.notifyScreenResize);

async function startCamera() {
    const video = document.getElementById("videoElement");
    try {
        const stream = await navigator.mediaDevices.getUserMedia({ video: true });
        video.srcObject = stream;
    } catch (err) {
        console.error("Error accessing camera: ", err);
        alert("Unable to access the camera.");
    }
}

// Capture photo
function capturePhoto() {

    // remove result-img, if exists
    const existingImg = document.getElementById("result_img");
    if (existingImg) {
        existingImg.remove();
    }

    const video = document.getElementById("videoElement");
    const canvas = document.getElementById("photoCanvas");
    const context = canvas.getContext("2d");

    // Set canvas dimensions to match the video feed
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;

    // Draw video frame onto the canvas
    context.drawImage(video, 0, 0, canvas.width, canvas.height);
}

// Event listeners
document.getElementById("captureButton").addEventListener("click", capturePhoto);

// Start the camera when the page loads
window.onload = startCamera;