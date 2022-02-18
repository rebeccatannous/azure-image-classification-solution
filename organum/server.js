var express = require('express');
const multer = require("multer");
var fs = require('fs');
const { v1: uuidv1 } = require('uuid');
const { BlobServiceClient } = require('@azure/storage-blob');
const CosmosClient = require("@azure/cosmos").CosmosClient;
const expressjs = require('express-ejs-layouts');
let ejs = require('ejs');
// Initialise Express
var app = express();
// Render static files
app.use(express.static('public'));
// Port website will run on
app.listen(8080);
app.use(expressjs);
app.set('view engine', 'ejs');
var uid = "";
var fruit = "";
var file_content = fs.readFileSync("./cart.json");
var content = JSON.parse(file_content);
for (item in content) {
    content[item] = 0;
}
fs.writeFileSync("./cart.json", JSON.stringify(content));

app.use(express.urlencoded({
    extended: true
}))

const upload = multer({
    dest: "./temp/"
        // you might also want to set some limits: https://github.com/expressjs/multer#limits
});

app.post("/scan-fruit",
    upload.single("img" /* name attribute of <file> element in your form */ ),
    (req, res) => {
        putBlob(req.file.path, res);
    }
);

app.post('/add-cart', (req, res) => {
    var file_content = fs.readFileSync("./cart.json");
    var content = JSON.parse(file_content);
    var amount = req.body.amount;
    for (items in content) {
        if (items == fruit) {
            content[items] = parseInt(content[items]) + parseInt(amount);
        }
    }
    fs.writeFileSync("./cart.json", JSON.stringify(content));
    res.redirect("./index.html");
});

app.get("/result", (req, res) => {
    var file_content = fs.readFileSync("./cart.json");
    var content = JSON.parse(file_content);
    res.render("./cart.ejs", { content: content });
});

async function putBlob(blob, res) {
    uid = uuidv1();
    const blobName = uid + '.jpg';
    //upload image to blob storage
    const blobServiceClient = BlobServiceClient.fromConnectionString("DefaultEndpointsProtocol=https;AccountName=project2storageacc;AccountKey=mZsfagt5Di6yoGDfYw6VB1njjQaj7JTCoKpW1jZi9Z0b9Cnr02c8+fte7v0nhVRj/BIurOHQOLHq+ASt7AIISw==;EndpointSuffix=core.windows.net");
    const containerName = 'images';
    const containerClient = blobServiceClient.getContainerClient(containerName);
    const blockBlobClient = containerClient.getBlockBlobClient(blobName);
    const data = fs.readFileSync(blob);
    const uploadBlobResponse = await blockBlobClient.upload(data, data.length);
    console.log(`Upload block blob ${blobName} successfully`);
    sleep(20000).then(() => {
        console.log("in");
        getBlob(res);
    });

}

async function getBlob(res) {
    var url = ""
    console.log("is it here?");
    const endpoint = "https://cosmosdbproject2.documents.azure.com:443/";
    key = "pcykDmNagmSr90SIB7b9WaTg05hoFFiRzyuaUyzyox5yk3EYWNOyrSIGQfWdRivLtpdTNcyByVdd1DPgUbygBw==";
    const client = new CosmosClient({ endpoint, key });
    const database = client.database("results");
    const container = database.container("CustomVision1");
    const querySpec = {
        query: "SELECT * FROM results WHERE results.id =" + "'" + uid + "'",
    };

    const { resources: items } = await container.items.query(querySpec).fetchAll();
    var bool = false

    items.forEach(item => {
        if (bool == false) {
            fruit = item.predictions[0].tagName;
            url = item.url;
            bool = true;
        }

        console.log("ehm");
        console.log(item.predictions[0].tagName);
    });
    bool = false;
    res.render("index.ejs", { fruit: fruit, url: url });
}

function sleep(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
}