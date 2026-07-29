document.addEventListener('DOMContentLoaded', function () {
    // Elementos del DOM
    const takePhotoBtn = document.getElementById('takePhotoBtn');
    const captureBtn = document.getElementById('captureBtn');
    const cancelCameraBtn = document.getElementById('cancelCameraBtn');
    const video = document.getElementById('video');
    const cameraContainer = document.getElementById('cameraContainer');
    const imagePreview = document.getElementById('imagePreview');
    const previewImage = document.getElementById('previewImage');
    const fotoBase64Input = document.getElementById('FotoBase64');
    const fileInput = document.getElementById('fileInput');

    let stream = null;

    
    takePhotoBtn.addEventListener('click', async function () {
        try {
            if (stream) {
                stream.getTracks().forEach(track => track.stop());
            }

            stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    facingMode: 'user',
                    width: { ideal: 200 },
                    height: { ideal: 200 }
                },
                audio: false
            });

            video.srcObject = stream;
            cameraContainer.style.display = 'block';
            takePhotoBtn.style.display = 'none';
            imagePreview.style.display = 'none'; 
        } catch (err) {
            console.error("Error al acceder a la cámara:", err);
            alert('No se pudo acceder a la cámara. Por favor, verifica los permisos.');
        }
    });

    captureBtn.addEventListener('click', function () {
        if (!stream) return;

        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        const ctx = canvas.getContext('2d');

        ctx.translate(canvas.width, 0);
        ctx.scale(-1, 1);
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

        
        let quality = 0.7;
        let imageData = canvas.toDataURL('image/jpeg', quality);

       
        while (calculateImageSize(imageData) > 20 && quality > 0.3) {
            quality -= 0.1;
            imageData = canvas.toDataURL('image/jpeg', quality);
        }

        previewImage.src = imageData;
        fotoBase64Input.value = imageData;
        imagePreview.style.display = 'block'; 

        stopCamera();
        cameraContainer.style.display = 'none';
        takePhotoBtn.style.display = 'inline-block';
    });

    cancelCameraBtn.addEventListener('click', function () {
        stopCamera();
        cameraContainer.style.display = 'none';
        takePhotoBtn.style.display = 'inline-block';
    });

    fileInput.addEventListener('change', function (e) {
        const file = e.target.files[0];
        if (file && (file.type === 'image/jpeg' || file.type === 'image/jpg')) {
            const reader = new FileReader();
            reader.onload = function (event) {
                previewImage.src = event.target.result;
                fotoBase64Input.value = event.target.result; 
                imagePreview.style.display = 'block';
            };
            reader.readAsDataURL(file);
        } else {
            alert('Por favor, sube un archivo JPG válido.');
        }
    });

    function stopCamera() {
        if (stream) {
            stream.getTracks().forEach(track => track.stop());
            stream = null;
        }
    }
    function calculateImageSize(imageData) {
        const base64Length = imageData.length - 'data:image/jpeg;base64,'.length;
        const sizeInBytes = 4 * Math.ceil(base64Length / 3) * 0.5624896334383812;
        return sizeInBytes / 1024; // Convertir a KB
    }

    window.addEventListener('beforeunload', function () {
        stopCamera();
    });
});