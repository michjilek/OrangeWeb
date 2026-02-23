//document.addEventListener("DOMContentLoaded", function () {
//    const screenWidth = window.innerWidth;
//    let imageToPreload;

//    switch (true) {
//        case (screenWidth <= 480):
//            imageToPreload = "_content/OP_Pages_Library/images/main_photo/main_photo_480.webp";
//            break;
//        case (screenWidth <= 1024):
//            imageToPreload = "_content/OP_Pages_Library/images/main_photo/main_photo_1024.webp";
//            break;
//        case (screenWidth <= 1920):
//            imageToPreload = "_content/OP_Pages_Library/images/main_photo/main_photo_1920.webp";
//            break;
//        default:
//            imageToPreload = "_content/OP_Pages_Library/images/main_photo/main_photo_2560.webp";
//    }

//    const preloadLink = document.createElement("link");
//    preloadLink.rel = "preload";
//    preloadLink.as = "image";
//    preloadLink.href = imageToPreload;

//    document.head.appendChild(preloadLink);
//});