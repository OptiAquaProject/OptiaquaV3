
// La identificación ahora es por sesión (login de administrador); ya no se piden
// credenciales en cada acción.
function ConfirmarEliminacion(idVersion,nivel) {
    var result = confirm("Está seguro de eliminar la versión: " + idVersion + " nivel: "+ nivel);
    if (result) {
        var paramJson = { idVersion ,nivel};
        callAction("/importacion/EliminarMapas/", {
            paramJson: paramJson
        }, function (res) {
            location.reload();
        });

    }
}


function BtnImportar() {
    event.preventDefault();
    var formdata = new FormData(); //FormData object
    var file = document.getElementById('formFileMapa');
    if (file.files.length != 1) {
        alert("Indique un fichero");
        return;
    }
    formdata.append(file.files[0].name, file.files[0]);

    var idVersion = document.getElementById('formIdVersion').value;
    if (idVersion == '') {
        alert("Indique versíón");
        return;
    }
    formdata.append("idVersion", idVersion);
    
    var nivel = document.getElementById('formNivel').value;
    if (nivel == '') {
        alert("Indique nivel");
        return;
    }
    formdata.append("nivel", nivel);

    //Creating an XMLHttpRequest and sending
    var xhr = new XMLHttpRequest();
    xhr.open('POST', urlPost);
    xhr.onreadystatechange = function () {
        if (xhr.readyState == 4 && xhr.status == 200) {
            if (xhr.response == 'OK') {
                location.reload();
            }
            else {
                alert(xhr.response);
                location.reload();
            }
        }
    };
    xhr.send(formdata);

    alert("La carga de los mapas puede ser lenta. La página se regargará automáticamente al finilizar la carga. Si lo desea puede recargar la página para seguir el proceso");
    //location.reload();
}


function sleep(delay) {
    var start = new Date().getTime();
    while (new Date().getTime() < start + delay);
}

function callAction(action, parametros, funcSuccess) {
    $.ajax({
        type: "POST",
        url: action,
        data: parametros,
        datatype: "json",
        async: true,
        contentType: 'application/x-www-form-urlencoded',
        success: function (respuesta) {
            try {
                if (respuesta.substring(0, 2) != "OK")
                    alert({ type: 'error', title: 'Oops...', text: respuesta })
                else
                    funcSuccess();

            } catch (err) {
                alert('Error en llamada al controlador');
            }
        },
        error: function () {
            alert('Error en llamada al controlador');
        }
    })
}

function getFormDataArray($form) {
    var unindexed_array = $form.serializeArray();
    var indexed_array = {};

    $.map(unindexed_array, function (n, i) {
        indexed_array[n['name']] = n['value'];
    });
    return indexed_array;
}

