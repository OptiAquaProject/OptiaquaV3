
function OnImportar() {
    var fileInput = document.getElementById('IdFileName');
    if (fileInput.files.length != 1) {
        alert("Indique un fichero excel");
        return;
    }

    var NifRegante = document.getElementById("NifRegante").value;
    var PassRegante = document.getElementById("PassRegante").value;


    var formdata = new FormData(); //FormData object    
    formdata.append(fileInput.files[0].name, fileInput.files[0]);
    formdata.append("NifRegante", NifRegante);
    formdata.append("PassRegante", PassRegante);

    //Creating an XMLHttpRequest and sending
    var xhr = new XMLHttpRequest();
    xhr.open('POST', urlPost);
    xhr.onreadystatechange = function () {
        if (xhr.readyState == 4 && xhr.status == 200) {
            PresentaResultados(xhr.response)
        }
    };
    xhr.send(formdata);
};


function PresentaResultados(respuestaJson) {
    var respuesta;
    var html;
    if (respuestaJson.substring(0, 2) == "OK") {
        var nImp = respuestaJson.substring(3);
        html = "<hr/>";
        html += "<h3>La importación se ha realizado de forma satisfactoria. Importados: " + nImp + "</h3>";
        document.getElementById("TablaErrores").innerHTML = html;
    } else if (respuestaJson.substring(0, 5) == "Error") {
        html = "<hr/>";
        html += "<h3>" + respuestaJson + "</h3>";
        document.getElementById("TablaErrores").innerHTML = html;
    }else {
        respuesta = JSON.parse(respuestaJson);
        html = "<hr/>";
    }
  
    html += "<h3>Lista de errores de la importación</h3>";
    html += "<h4><small>No se ha importado ningún registro.</small></h4>";
    html += "<table class='table table-hover'>";
    html += "<tr>";
    html += " <th scope='col' width='5%'>#Lin</th>";
    html += " <th scope='col' width='95%'>Descripción del error</th>";
    html += "</tr>";
    for (i = 0; i < respuesta.length; i++) {
        html += "<tr>";
        html += "<td>" + respuesta[i].NLinea + "</td>";
        html += "<td>" + respuesta[i].Descripcion + "</td>";
        html += "</tr>";
    }
    html = html + "</table>"
    document.getElementById("TablaErrores").innerHTML = html;
}
